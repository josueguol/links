using FluentValidation;

namespace Links.Api.Modules.Posts;

public sealed class CreatePostRequestValidator : AbstractValidator<CreatePostRequest>
{
    public CreatePostRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required.")
            .MaximumLength(5000).WithMessage("Content must not exceed 5000 characters.");
    }
}
