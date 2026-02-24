using FluentValidation;
using PottaAPI.Models;

namespace PottaAPI.Validators
{
    /// <summary>
    /// Validator for AddNotesRequest
    /// </summary>
    public class AddNotesValidator : AbstractValidator<AddNotesRequest>
    {
        public AddNotesValidator()
        {
            RuleFor(x => x.TransactionId)
                .NotEmpty()
                .WithMessage("Transaction ID is required")
                .MaximumLength(50)
                .WithMessage("Transaction ID cannot exceed 50 characters");

            RuleFor(x => x.NoteText)
                .NotEmpty()
                .WithMessage("Note text is required")
                .MaximumLength(500)
                .WithMessage("Note text cannot exceed 500 characters")
                .Must(text => !string.IsNullOrWhiteSpace(text))
                .WithMessage("Note text cannot be only whitespace");

            // AddedByStaffId is optional and not used by the service, so no validation needed
        }
    }
}
