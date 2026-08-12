using FluentValidation;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.ReorderFaqs;

public sealed class ReorderFaqsCommandValidator : AbstractValidator<ReorderFaqsCommand>
{
    public ReorderFaqsCommandValidator()
    {
        RuleFor(x => x.Entries).NotEmpty();
        RuleFor(x => x.Entries)
            .Must(entries => entries.Select(e => e.Id).Distinct().Count() == entries.Count)
            .WithMessage("Duplicate FAQ ids are not allowed in the same reorder request.");
    }
}
