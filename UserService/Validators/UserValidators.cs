using FluentValidation;
using UserService.DTOs;

namespace UserService.Validators
{
    // Solución de la Tarea 2 (B.3): la forma correcta de los datos de entrada.
    public class CreateUserValidator : AbstractValidator<CreateUserDto>
    {
        public CreateUserValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required")
                .MinimumLength(2).WithMessage("Name must be at least 2 characters")
                .MaximumLength(50).WithMessage("Name must not exceed 50 characters");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Last name is required")
                .MinimumLength(2).WithMessage("Last name must be at least 2 characters")
                .MaximumLength(50).WithMessage("Last name must not exceed 50 characters");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters")
                .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
                .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
                .Matches(@"[0-9]").WithMessage("Password must contain at least one digit");
        }
    }

    public class UpdateUserValidator : AbstractValidator<UpdateUserDto>
    {
        public UpdateUserValidator()
        {
            // Todos los campos son opcionales: solo se valida el que trae valor
            RuleFor(x => x.Email)
                .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
                .WithMessage("Invalid email format");

            RuleFor(x => x.Name)
                .MinimumLength(2).When(x => !string.IsNullOrEmpty(x.Name))
                .MaximumLength(50).When(x => !string.IsNullOrEmpty(x.Name));

            RuleFor(x => x.LastName)
                .MinimumLength(2).When(x => !string.IsNullOrEmpty(x.LastName))
                .MaximumLength(50).When(x => !string.IsNullOrEmpty(x.LastName));

            RuleFor(x => x.Password)
                .MinimumLength(8).When(x => !string.IsNullOrEmpty(x.Password))
                .Matches(@"[A-Z]").When(x => !string.IsNullOrEmpty(x.Password))
                .Matches(@"[a-z]").When(x => !string.IsNullOrEmpty(x.Password))
                .Matches(@"[0-9]").When(x => !string.IsNullOrEmpty(x.Password));
        }
    }
}
