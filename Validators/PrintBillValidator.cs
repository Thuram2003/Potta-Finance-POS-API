using FluentValidation;
using PottaAPI.Models;

namespace PottaAPI.Validators
{
    public class PrintBillValidator : AbstractValidator<PrintBillRequest>
    {
        public PrintBillValidator()
        {
            RuleFor(x => x.TransactionId)
                .NotEmpty().WithMessage("Transaction ID is required")
                .MaximumLength(50).WithMessage("Transaction ID cannot exceed 50 characters");
                
            RuleFor(x => x.StaffId)
                .GreaterThan(0).WithMessage("Valid staff ID is required");
                
            RuleFor(x => x.Notes)
                .MaximumLength(200).WithMessage("Notes cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.Notes));
        }
    }
}
