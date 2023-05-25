using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using MyApp.Models;
using MyApp.DataAccessLayer.Infrastructer.IRepository;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace MyWebApp.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public HomeController(ILogger<HomeController> logger, IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
        }


        public IActionResult Index(string Searchtext)
        {
            IEnumerable<Product> products = _unitOfWork.Product.GetAll(includeProperties: "Category");

            if (!String.IsNullOrEmpty(Searchtext))
            {
                products = products.Where(p => p.Name.Contains(Searchtext) || p.Category.Name.Contains(Searchtext));
            }

            ViewData["CurrentFilter"] = Searchtext;
            return View(products);
        }


        //public IActionResult Index(string SearchText = "")
        //{


        //    List<Product> products;
        //    if (SearchText != "" && SearchText != null)
        //    {
        //        products = _unitOfWork.Product.Where(p => p..Contain
        //    }
        //    else
        //        products = _unitOfWork.Product.ToList();

        //        return View(products);
        //}
        [HttpGet]
        public IActionResult Details(int? ProductId)
        {
            Cart cart = new Cart()
            {
                Product = _unitOfWork.Product.GetT(x => x.Id == ProductId, includeProperties: "Category"),
                Count = 1,
                ProductId= (int)ProductId
            };
          
            return View(cart);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult Details(Cart cart)
        {
            if (ModelState.IsValid)
            {

                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var claims = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
                cart.ApplicationUserId = claims.Value;

                var cartItem = _unitOfWork.Cart.GetT(x=>x.ProductId == cart.ProductId &&
                x.ApplicationUserId==claims.Value);

                if (cartItem==null)
                {
                    _unitOfWork.Cart.Add(cart);
                    _unitOfWork.Save();
                    HttpContext.Session.SetInt32("SessionCart",_unitOfWork
                        .Cart.GetAll(x=>x.ApplicationUserId==claims.Value).ToList().Count);
                    
                }
                else
                {
                    _unitOfWork.Cart.IncrementCartItem(cartItem, cart.Count);
                    _unitOfWork.Save();
                }
               
            }

               
            
            //Cart cart = new Cart()
            //{
            //    Product = _unitOfWork.Product.GetT(x => x.Id == id, includeProperties: "Category"),
            //    Count = 1
            //};

            return RedirectToAction("Index");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new MyApp.Models.ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}