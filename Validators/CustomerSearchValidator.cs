using FluentValidation;
using PottaAPI.Models;

namespace PottaAPI.Validators
{
    // Validates customer search requests
    public class CustomerSearchValidator : AbstractValidator<CustomerSearchDto>
    {
        public CustomerSearchValidator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0)
                .WithMessage("Page must be greater than 0");

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100)
                .WithMessage("PageSize must be between 1 and 100");

            RuleFor(x => x.SearchTerm)
                .MaximumLength(100)
                .When(x => !string.IsNullOrEmpty(x.SearchTerm))
                .WithMessage("Search term cannot exceed 100 characters");
        }
    }
}
