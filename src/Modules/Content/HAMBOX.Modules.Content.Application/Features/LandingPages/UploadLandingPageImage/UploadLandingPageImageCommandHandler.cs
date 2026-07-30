using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.UploadLandingPageImage;

internal sealed class UploadLandingPageImageCommandHandler(IFileStorage fileStorage)
    : IRequestHandler<UploadLandingPageImageCommand, Result<string>>
{
    public async Task<Result<string>> Handle(UploadLandingPageImageCommand request, CancellationToken cancellationToken)
    {
        if (request.FileSizeBytes <= 0 || request.FileSizeBytes > fileStorage.MaxFileSizeBytes)
        {
            return Result.Failure<string>(ContentErrors.InvalidImage);
        }

        if (!fileStorage.IsAllowedContentType(request.ContentType))
        {
            return Result.Failure<string>(ContentErrors.InvalidImage);
        }

        StoredFileResult storedFile;

        try
        {
            storedFile = await fileStorage.SaveAsync(
                request.Content,
                request.FileName,
                request.ContentType,
                "page-builder",
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<string>(ContentErrors.InvalidImage);
        }

        return Result.Success(storedFile.PublicUrl);
    }
}
