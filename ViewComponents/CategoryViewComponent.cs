using Microsoft.AspNetCore.Mvc;
using MyApp.DataAccessLayer.Infrastructer.IRepository;

namespace MyWebApp.ViewComponents
{
    public class CategoryViewComponent : ViewComponent
    {
        private readonly IUnitOfWork _unitOfWork;
        public CategoryViewComponent(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public IViewComponentResult Invoke()
        {
            var categories = _unitOfWork.Category.GetAll().OrderBy(c => c.Name).ToList();
            return View(categories);
        }
    }
}
