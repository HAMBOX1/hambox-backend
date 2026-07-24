using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Instructions.UploadProductInstructionsImage;

public sealed record UploadProductInstructionsImageCommand(
    Guid ProductId,
    Stream Content,
    string FileName,
    string ContentType,
    long FileSizeBytes) : IRequest<Result<string>>;
