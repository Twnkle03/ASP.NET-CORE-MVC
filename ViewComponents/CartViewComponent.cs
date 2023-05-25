using Microsoft.AspNetCore.Mvc;
using MyApp.DataAccessLayer.Infrastructer.IRepository;
using System.Security.Claims;

namespace MyWebApp.ViewComponents
{
    public class CartViewComponent : ViewComponent
    {
        private readonly IUnitOfWork _unitOfWork;

        public CartViewComponent(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claims = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            if (claims != null)
            {
                if(HttpContext.Session.GetInt32("SessionCart")!=null) 
                {
                    return View(HttpContext.Session.GetInt32("SessionCart"));
                }
                else
                {
                    HttpContext.Session.SetInt32("SessionCart", _unitOfWork
                        .Cart.GetAll(x => x.ApplicationUserId == claims.Value).ToList().Count);
                    return View(HttpContext.Session.GetInt32("Session32"));
                }
            }
            else
            {
                HttpContext.Session.Clear();
                return View(0);
            }
           
        }
    }
}
