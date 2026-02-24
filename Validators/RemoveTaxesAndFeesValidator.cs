using FluentValidation;
using PottaAPI.Models;

namespace PottaAPI.Validators
{
    /// <summary>
    /// Validator for RemoveTaxesAndFeesRequest
    /// </summary>
    public class RemoveTaxesAndFeesValidator : AbstractValidator<RemoveTaxesAndFeesRequest>
    {
        public RemoveTaxesAndFeesValidator()
        {
            RuleFor(x => x.TransactionId)
                .NotEmpty().WithMessage("Transaction ID is required")
                .MaximumLength(50).WithMessage("Transaction ID cannot exceed 50 characters");

            RuleFor(x => x.StaffId)
                .GreaterThan(0).WithMessage("Staff ID must be greater than 0");

            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("Reason is required")
                .MaximumLength(200).WithMessage("Reason cannot exceed 200 characters");
        }
    }
}
