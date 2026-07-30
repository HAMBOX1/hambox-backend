using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.LandingPages.UploadLandingPageImage;

public sealed record UploadLandingPageImageCommand(
    Stream Content,
    string FileName,
    string ContentType,
    long FileSizeBytes) : IRequest<Result<string>>;
