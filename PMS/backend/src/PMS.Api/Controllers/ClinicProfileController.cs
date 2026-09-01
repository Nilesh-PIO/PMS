using Microsoft.AspNetCore.Mvc;
using PMS.Api.Filters;
using PMS.Application.Abstractions;
using PMS.Application.Dtos.Clinic;
using PMS.Application.Exceptions;
using PMS.Application.Services;

namespace PMS.Api.Controllers;

/// <summary>
/// F-3's four endpoints (planning-pms-verification.md, F-3 point 3). Depends on
/// <see cref="IClinicProfileService"/> and never on PmsDbContext (section 2, API shape).
/// </summary>
/// <remarks>
/// No <c>[AllowAnonymous]</c> anywhere: F-2's fallback policy is default-deny, so every route
/// here requires the session cookie exactly as the plan's table specifies.
/// </remarks>
[ApiController]
[Route("api/clinic-profile")]
[Produces("application/json")]
public class ClinicProfileController : ControllerBase
{
    /// <summary>
    /// A little headroom over the 200 KB business cap for multipart framing, so an image that is
    /// legitimately just under the limit is not rejected by the transport before the service can
    /// judge it - and so a 40 MB upload is refused at the pipeline rather than buffered into
    /// memory first.
    /// </summary>
    private const int MultipartBodyLimitBytes = ClinicProfileService.MaxSignatureBytes + (32 * 1024);

    private readonly IClinicProfileService _clinicProfile;

    public ClinicProfileController(IClinicProfileService clinicProfile)
    {
        _clinicProfile = clinicProfile;
    }

    /// <summary>
    /// The clinic profile. 404 when first-run setup has never been saved - which is not an error
    /// but the state the client renders as a blank setup form (E-1).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ClinicProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicProfileResponse>> Get(CancellationToken cancellationToken)
    {
        var profile = await _clinicProfile.GetAsync(cancellationToken);

        if (profile is null)
        {
            // 404, not an empty 200. "Setup has never been run" and "setup was run and left
            // blank" are different states, and the client's first-run gate depends on telling
            // them apart (E-1).
            throw new NotFoundException(
                ClinicProfileService.EntityType,
                Domain.Entities.ClinicProfile.SingletonId.ToString());
        }

        return Ok(profile);
    }

    /// <summary>Creates or updates the clinic profile. 200 on success, 400 on validation failure.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ClinicProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClinicProfileResponse>> Upsert(
        [FromBody] UpsertClinicProfileRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _clinicProfile.UpsertAsync(request, cancellationToken));

    /// <summary>
    /// Uploads the signature image. 200 on success, 400 for a non-PNG or empty file,
    /// 413 above the 200 KB cap.
    /// </summary>
    /// <remarks>
    /// The form is read by hand rather than bound to an <c>IFormFile</c> parameter, for one
    /// reason worth stating: when the request exceeds the size limit, MVC's form value provider
    /// catches the transport failure and records it in <c>ModelState</c>, and
    /// <c>[ApiController]</c> then answers <b>400</b> - the wrong status, and one that tells the
    /// physician their form is malformed when in fact their file is too big. Reading the form
    /// here lets that failure become the 413 the plan's route table specifies.
    /// </remarks>
    [HttpPost("signature")]
    [RequestSizeLimit(MultipartBodyLimitBytes)]
    [MaxUploadBytes(
        MultipartBodyLimitBytes,
        Message = "The signature image must be 200 KB or smaller.")]
    [ProducesResponseType(typeof(ClinicProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<ClinicProfileResponse>> UploadSignature(
        CancellationToken cancellationToken)
    {
        IFormCollection form;
        try
        {
            form = await Request.ReadFormAsync(cancellationToken);
        }
        catch (BadHttpRequestException ex)
            when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // A chunked upload with no Content-Length: the limit is only hit while reading.
            throw TooLarge();
        }
        catch (InvalidDataException)
        {
            throw TooLarge();
        }
        catch (InvalidOperationException)
        {
            // Not a form request at all.
            throw new ValidationFailedException("file", "Choose a signature image to upload.");
        }

        var file = form.Files["file"];

        if (file is null || file.Length == 0)
        {
            throw new ValidationFailedException("file", "Choose a signature image to upload.");
        }

        if (file.Length > ClinicProfileService.MaxSignatureBytes)
        {
            throw TooLarge();
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);

        return Ok(await _clinicProfile.SetSignatureAsync(buffer.ToArray(), cancellationToken));
    }

    private static PayloadTooLargeException TooLarge() => new(
        $"The signature image must be {ClinicProfileService.MaxSignatureBytes / 1024} KB or smaller.",
        ClinicProfileService.MaxSignatureBytes);

    /// <summary>
    /// Removes the stored signature. The printed footer falls back to a ruled signature area -
    /// never a broken-image placeholder (plan F-3 point 1).
    /// </summary>
    [HttpDelete("signature")]
    [ProducesResponseType(typeof(ClinicProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicProfileResponse>> DeleteSignature(
        CancellationToken cancellationToken) =>
        Ok(await _clinicProfile.ClearSignatureAsync(cancellationToken));
}
