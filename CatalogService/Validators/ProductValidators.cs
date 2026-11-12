using CatalogService.DTOs;
using FluentValidation;

namespace CatalogService.Validators
{
    // Demo 2 de la Semana 2: la forma correcta de un producto nuevo.
    public class CreateProductValidator : AbstractValidator<CreateProductDto>
    {
        public CreateProductValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required")
                .MinimumLength(3).WithMessage("Name must be at least 3 characters");
            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Price must be greater than 0");
            RuleFor(x => x.Stock)
                .GreaterThanOrEqualTo(0).WithMessage("Stock cannot be negative");
            RuleFor(x => x.Category)
                .NotEmpty().WithMessage("Category is required");
        }
    }
}
