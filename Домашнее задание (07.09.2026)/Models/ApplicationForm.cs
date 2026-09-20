using System.ComponentModel.DataAnnotations;

namespace Домашнее_задание__07._09._2026_.Models
{
    public class ApplicationForm
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Введите имя")]
        [Display(Name = "Имя")]
        public string FirstName { get; set; } = "";

        [Required(ErrorMessage = "Введите фамилию")]
        [Display(Name = "Фамилия")]
        public string LastName { get; set; } = "";

        [Required(ErrorMessage = "Введите email")]
        [EmailAddress(ErrorMessage = "Некорректный email")]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Введите возраст")]
        [Range(14, 100, ErrorMessage = "Возраст от 14 до 100")]
        [Display(Name = "Возраст")]
        public int Age { get; set; }

        [Display(Name = "Город")]
        public string? City { get; set; }

        [Display(Name = "О себе")]
        public string? About { get; set; }
    }
}
