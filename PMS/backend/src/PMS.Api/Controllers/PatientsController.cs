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
    private readonly IPatientDuplicateService _duplicates;

    public PatientsController(IPatientService patients, IPatientDuplicateService duplicates)
    {
        _patients = patients;
        _duplicates = duplicates;
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
    /// <b>F-6 now lives on this route too.</b> Without <c>?confirmDuplicate=true</c>, a registration
    /// that matches someone already on file is answered with <b>409</b> carrying the candidate list
    /// in the problem body, and <em>nothing is written</em>. Repeating the request with
    /// <c>confirmDuplicate=true</c> is the physician saying "I looked, register them anyway", and it
    /// always succeeds — the check warns and never blocks (REC-2).
    /// </para>
    /// </remarks>
    /// <param name="request">The registration form.</param>
    /// <param name="confirmDuplicate">
    /// F-6. Set to <c>true</c> to register despite a duplicate warning. Defaults to <c>false</c>, so
    /// the check is on by omission — a caller cannot skip it by forgetting about it.
    /// </param>
    /// <param name="cancellationToken">Cancellation.</param>
    [HttpPost]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PatientResponse>> Create(
        [FromBody] CreatePatientRequest request,
        [FromQuery] bool confirmDuplicate,
        CancellationToken cancellationToken)
    {
        var created = await _patients.CreateAsync(request, confirmDuplicate, cancellationToken);

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

    // --- F-6 (plan F-6 point 3) ---------------------------------------------

    /// <summary>
    /// Asks whether this person may already be on file. Always 200, with a possibly-empty list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A POST that writes nothing, deliberately.</b> The plan specifies POST, and it is the right
    /// verb here for a reason beyond the plan: the arguments are a patient's name, phone number and
    /// date of birth, and a GET would put all three in the URL, where they land in server logs,
    /// browser history and any proxy in between. A request body keeps identifiable patient data out
    /// of places nobody thinks of as a database.
    /// </para>
    /// <para>
    /// <b>Never an error status when nothing matches.</b> "No duplicates" is the ordinary answer and
    /// a 404 for it would make the client treat the common case as a failure.
    /// </para>
    /// </remarks>
    [HttpPost("duplicate-check")]
    [ProducesResponseType(typeof(IReadOnlyList<DuplicateCandidateResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DuplicateCandidateResponse>>> CheckDuplicates(
        [FromBody] DuplicateCheckRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _duplicates.FindCandidatesAsync(request, cancellationToken));

    /// <summary>
    /// Records that this patient is a duplicate of another. 200 with the marked record, 400 for a
    /// bad target, 404 if this patient does not exist, 409 for a cycle or an existing pointer.
    /// </summary>
    /// <remarks>
    /// <b>Nothing is deleted and nothing is overwritten on either record</b> (plan F-6 point 5,
    /// E-26, E-33). This writes a pointer and a status on the marked record and touches the survivor
    /// not at all; every visit stays attached to the patient it was recorded against and stays
    /// queryable. It is deliberately <em>not</em> a DELETE, and there is no DELETE anywhere in this
    /// controller — the record being retired is the one carrying a patient's history.
    /// </remarks>
    [HttpPost("{id:guid}/mark-merged")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PatientResponse>> MarkMerged(
        Guid id,
        [FromBody] MarkMergedRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _duplicates.MarkMergedAsync(id, request, cancellationToken));
}
