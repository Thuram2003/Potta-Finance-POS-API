using FluentValidation;
using PottaAPI.Models;

namespace PottaAPI.Validators
{
    /// <summary>
    /// Validator for ShiftHandoverRequest
    /// </summary>
    public class ShiftHandoverValidator : AbstractValidator<ShiftHandoverRequest>
    {
        public ShiftHandoverValidator()
        {
            RuleFor(x => x.CurrentStaffId)
                .GreaterThan(0).WithMessage("Current staff ID must be greater than 0");

            RuleFor(x => x.NewStaffId)
                .GreaterThan(0).WithMessage("New staff ID must be greater than 0");

            RuleFor(x => x.NewStaffId)
                .NotEqual(x => x.CurrentStaffId)
                .WithMessage("New staff ID must be different from current staff ID");

            RuleFor(x => x.Reason)
                .MaximumLength(200).WithMessage("Reason cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.Reason));
        }
    }
}
