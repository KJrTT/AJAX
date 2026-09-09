using FluentValidation;

namespace MyApi;

/// <summary>
/// Валидатор для ProductRequest
/// </summary>
public class ProductRequestValidator : AbstractValidator<ProductRequestV1>
{
    public ProductRequestValidator()
    {
        // Имя обязательно и не длиннее 100 символов
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Название продукта обязательно")
            .MaximumLength(100).WithMessage("Название не может быть длиннее 100 символов");

        // Описание опционально, но не длиннее 500 символов
        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Описание не может быть длиннее 500 символов");

        // Цена должна быть больше 0
        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Цена должна быть больше 0");

        // Категория обязательна
        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Категория обязательна");

        // Сток не может быть отрицательным
        RuleFor(x => x.Stock)
            .GreaterThanOrEqualTo(0).WithMessage("Остаток не может быть отрицательным");

        // Атрибуты: проверяем, что если есть, то ключи не пустые
        RuleForEach(x => x.Attributes)
            .Must(kvp => !string.IsNullOrEmpty(kvp.Key))
            .WithMessage("Ключ атрибута не может быть пустым");
    }
}