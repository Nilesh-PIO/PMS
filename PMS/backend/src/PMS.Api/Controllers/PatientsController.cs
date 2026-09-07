using Microsoft.AspNetCore.Mvc;
using PMS.Application.Abstractions;
using PMS.Application.Dtos.Patients;

namespace PMS.Api.Controllers;

/// <summary>
/// F-5's two endpoints (planning-pms-verification.md, F-5 point 3). Depends on
/// <see cref="IPatientService"/> and never on PmsDbContext (section 2, API shape).
/// </summary>
/// <remarks>
/// No <c>[AllowAnonymous]</c> anywhere. F-2's fallback policy is default-deny, so both routes
/// require the session cookie by omission rather than by opt-in - which is the point of that
/// design: the first controller that serves real patient data is protected because nobody had to
/// remember to protect it.
/// </remarks>
[ApiController]
[Route("api/patients")]
[Produces("application/json")]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _patients;

    public PatientsController(IPatientService patients)
    {
        _patients = patients;
    }

    /// <summary>
    /// Registers a patient. 201 with the new record, 400 with field errors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Replaying a submission token returns 201 with the patient that token already created</b>
    /// (E-43, E-46), rather than creating a second one and rather than reporting a conflict. Both
    /// alternatives are worse for the person holding the mouse: a duplicate splits the patient's
    /// history, and a 409 tells someone whose registration actually succeeded that it failed.
    /// Identical request, identical response - which is what idempotent means from the caller's
    /// side.
    /// </para>
    /// <para>
    /// The plan's route table also lists <b>409 (duplicate-confirmation required)</b> for this
    /// endpoint. That status belongs to <b>F-6</b>, which adds the name/phone/DOB similarity check
    /// and the <c>?confirmDuplicate=true</c> escape hatch; F-5 ships the registration path it will
    /// hang off. Nothing here returns 409 yet, and no client should be written to expect it until
    /// F-6 lands.
    /// </para>
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PatientResponse>> Create(
        [FromBody] CreatePatientRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _patients.CreateAsync(request, cancellationToken);

        // CreatedAtAction so the response carries a Location header pointing at the profile - the
        // client's next navigation is exactly that URL.
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>The full profile. 200, or 404 if no patient has this id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PatientDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientDetailResponse>> Get(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await _patients.GetAsync(id, cancellationToken));
}
