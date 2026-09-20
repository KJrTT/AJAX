using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Домашнее_задание__07._09._2026_.Models;

namespace Домашнее_задание__07._09._2026_.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet]
        [HttpPost]
        public IActionResult Index(ApplicationForm? form)
        {
            if (Request.Method == "GET")
                return View(new ApplicationForm());

            if (!ModelState.IsValid)
                return View(form);

            form!.Id = ApplicationStorage.NextId++;
            ApplicationStorage.Items.Add(form);

            return View("Details", form);
        }

        public IActionResult Details(int id)
        {
            var item = ApplicationStorage.Items.FirstOrDefault(x => x.Id == id);
            if (item == null)
                return RedirectToAction("Index");

            return View(item);
        }
    }
}
