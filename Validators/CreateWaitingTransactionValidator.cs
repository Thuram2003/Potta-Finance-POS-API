using FluentValidation;
using PottaAPI.Models;

namespace PottaAPI.Validators
{
    // Validates waiting transaction creation
    public class CreateWaitingTransactionValidator : AbstractValidator<CreateWaitingTransactionDto>
    {
        public CreateWaitingTransactionValidator()
        {
            RuleFor(x => x.StaffId)
                .GreaterThan(0)
                .WithMessage("StaffId must be greater than 0");

            RuleFor(x => x.Items)
                .NotEmpty()
                .WithMessage("Order must contain at least one item")
                .Must(items => items != null && items.Any())
                .WithMessage("Items list cannot be null or empty");

            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x.ProductId)
                    .NotEmpty()
                    .WithMessage("ProductId is required for each item");

                item.RuleFor(x => x.Name)
                    .NotEmpty()
                    .WithMessage("Product name is required for each item")
                    .MaximumLength(200)
                    .WithMessage("Product name cannot exceed 200 characters");

                item.RuleFor(x => x.Quantity)
                    .GreaterThan(0)
                    .WithMessage("Quantity must be greater than 0");

                item.RuleFor(x => x.Price)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("Price cannot be negative");

                item.RuleFor(x => x.SubTotal)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("SubTotal cannot be negative");
            });

            RuleFor(x => x.TableId)
                .NotEmpty()
                .When(x => !string.IsNullOrEmpty(x.TableId))
                .WithMessage("TableId must be valid if provided")
                .MaximumLength(50)
                .WithMessage("TableId cannot exceed 50 characters");

            RuleFor(x => x.CustomerId)
                .NotEmpty()
                .When(x => !string.IsNullOrEmpty(x.CustomerId))
                .WithMessage("CustomerId must be valid if provided")
                .MaximumLength(50)
                .WithMessage("CustomerId cannot exceed 50 characters");
        }
    }
}
