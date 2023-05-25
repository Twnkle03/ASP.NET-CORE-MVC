﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic;
using MyApp.CommonHelper;
using MyApp.DataAccessLayer.Infrastructer.IRepository;
using MyApp.DataAccessLayer.Infrastructer.Repository;
using MyApp.Models;
using MyApp.Models.ViewModels;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;

namespace MyWebApp.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {
        private IUnitOfWork _unitOfWork;
        public CartVM vm { get; set; }

        public CartController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claims = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            vm = new CartVM()
            {
                ListOfCart = _unitOfWork.Cart.GetAll(x => x.ApplicationUserId == claims.Value, includeProperties: "Product"),
                OrderHeader = new MyApp.Models.OrderHeader()
            };
          
            foreach (var item in vm.ListOfCart)
            {
                vm.OrderHeader.OrderTotal += (item.Product.Price * item.Count);
            }


            return View(vm);
        }

        public IActionResult Summary()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claims = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            vm = new CartVM()
            {
                ListOfCart = _unitOfWork.Cart.GetAll(x => x.ApplicationUserId == claims.Value, includeProperties: "Product"),
                OrderHeader = new MyApp.Models.OrderHeader()
            };
            vm.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser.GetT(x => x.Id == claims.Value);
            vm.OrderHeader.Name = vm.OrderHeader.ApplicationUser.Name;
            vm.OrderHeader.Phone = vm.OrderHeader.ApplicationUser.PhoneNumber;
            vm.OrderHeader.Address = vm.OrderHeader.ApplicationUser.Address;
            vm.OrderHeader.City = vm.OrderHeader.ApplicationUser.City;
            vm.OrderHeader.State = vm.OrderHeader.ApplicationUser.State;
            vm.OrderHeader.PostalCode = vm.OrderHeader.ApplicationUser.Pincode;


            foreach (var item in vm.ListOfCart)
            {
                vm.OrderHeader.OrderTotal += (item.Product.Price * item.Count);
            }

            return View(vm);
        }

        [HttpPost]
        
        public IActionResult Summary(CartVM vm)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claims = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            vm.ListOfCart = _unitOfWork.Cart.GetAll(x => x.ApplicationUserId == claims.Value, includeProperties: "Product");
            vm.OrderHeader.OrderStatus = OrderStatus.StatusPending;
            vm.OrderHeader.PaymentStatus = PaymentStatus.StatusPending;
            vm.OrderHeader.DateOfOrder = DateTime.Now;
            vm.OrderHeader.ApplicationUserId = claims.Value;

            foreach (var item in vm.ListOfCart)
            {
                vm.OrderHeader.OrderTotal += (item.Product.Price * item.Count);
            }
            _unitOfWork.OrderHeader.Add(vm.OrderHeader);
            _unitOfWork.Save();
            foreach (var item in vm.ListOfCart)
            {
                OrderDetail orderDetail = new OrderDetail()
                {
                    ProductId = item.ProductId,
                    OrderHeaderId = vm.OrderHeader.Id,
                    Count = item.Count,
                    Price = item.Product.Price,
                };
                _unitOfWork.OrderDetail.Add(orderDetail);
                _unitOfWork.Save();
            }

            var domain = "https://localhost:7250/";
            var options = new SessionCreateOptions
            {
                LineItems = new List<SessionLineItemOptions>(),
                Mode = "payment",
                SuccessUrl = domain+$"customer/cart/ordersuccess?id={vm.OrderHeader.Id}",
                CancelUrl = domain+$"customer/cart/Index",
            };

            foreach (var item in vm.ListOfCart)
            {

                var lineItemsOptions = new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.Product.Price*100),
                        Currency = "INR",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product.Name,
                        },
                    },
                    Quantity = item.Count,
                };
                options.LineItems.Add(lineItemsOptions);
                
            }
            
            var service = new SessionService();
            Session session = service.Create(options);
            _unitOfWork.OrderHeader.PaymentStatus(vm.OrderHeader.Id,session.Id,session.PaymentIntentId);
            _unitOfWork.Save();

            _unitOfWork.Cart.DeleteRange(vm.ListOfCart);
            _unitOfWork.Save();

            Response.Headers.Add("Location", session.Url);
           return new StatusCodeResult(303);

            
            return RedirectToAction("Index","Home");
        }

        public IActionResult ordersuccess(int id)
        {
            var orderHeader = _unitOfWork.OrderHeader.GetT(x=>x.Id==id);
            var sessionService = new SessionService();
            var paymentIntentService = new PaymentIntentService();
         
            Session session = sessionService.Get(orderHeader.SessionId);
            PaymentIntent paymentIntent = paymentIntentService.Get(session.PaymentIntentId);
            // Update the OrderHeader entity in the database with both paymentIntentId and sessionId
            _unitOfWork.OrderHeader.Update(orderHeader);
            orderHeader.PaymentIntentId = paymentIntent.Id;
            orderHeader.SessionId = session.Id;
            orderHeader.PaymentStatus = paymentIntent.Status;
            _unitOfWork.Save();

            if (paymentIntent.Status.ToLower() == "succeeded")
            {
                _unitOfWork.OrderHeader.UpdateStatus(id, OrderStatus.StatusApproved, PaymentStatus.StatusApproved);
            }

            List<Cart> cart = _unitOfWork.Cart.GetAll(x => x.ApplicationUserId == orderHeader.ApplicationUserId).ToList();
            _unitOfWork.Cart.DeleteRange(cart);
            _unitOfWork.Save();

            return View(id);
        }

        public IActionResult plus(int id) 
        {
           var cart = _unitOfWork.Cart.GetT(x=>x.Id== id);
            _unitOfWork.Cart.IncrementCartItem(cart, 1);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult minus(int id)
        {
            var cart = _unitOfWork.Cart.GetT(x => x.Id == id);

            if (cart.Count <= 1)
            {
                _unitOfWork.Cart.Delete(cart);
                var count = _unitOfWork.Cart.GetAll(x => x.ApplicationUserId == cart.ApplicationUserId)
               .ToList().Count-1;
                HttpContext.Session.SetInt32("SessionCart", count);
            }
            else
            {
                _unitOfWork.Cart.DecrementCartItem(cart, 1);
            }
            //_unitOfWork.Cart.DecrementCartItem(cart, 1);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult delete(int id)
        {
            var cart = _unitOfWork.Cart.GetT(x => x.Id == id);
            _unitOfWork.Cart.Delete(cart);
            _unitOfWork.Save();
            var count = _unitOfWork.Cart.GetAll(x => x.ApplicationUserId == cart.ApplicationUserId)
                .ToList().Count;
            HttpContext.Session.SetInt32("SessionCart", count);
            return RedirectToAction(nameof(Index));
        }
    }
}
