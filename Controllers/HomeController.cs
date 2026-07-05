using Microsoft.AspNetCore.Mvc;

namespace Website_API.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
