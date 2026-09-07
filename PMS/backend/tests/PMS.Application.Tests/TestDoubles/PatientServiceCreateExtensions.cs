using PMS.Application.Dtos.Patients;
using PMS.Application.Services;

namespace PMS.Application.Tests.TestDoubles;

/// <summary>
/// Keeps F-5's existing <c>CreateAsync(request, token)</c> call sites compiling after F-6 added the
/// <c>confirmDuplicate</c> parameter.
/// </summary>
/// <remarks>
/// <para>
/// <b>It forwards <c>confirmDuplicate: false</c> — the production default — on purpose.</b> The
/// tempting shortcut was to pass <c>true</c> and make every inherited F-5 test skip the duplicate
/// check outright, which would have compiled just as well and quietly removed those forty-odd tests
/// from covering the interaction between the two features. Passing <c>false</c> means F-5's whole
/// suite now runs through F-6's check on the way to the insert, so if the check ever starts
/// refusing an ordinary registration, F-5's tests are the ones that say so.
/// </para>
/// <para>
/// A test-assembly extension rather than an overload on the service: production code should have
/// exactly one way to register a patient, and that way should require the caller to have an opinion
/// about duplicates.
/// </para>
/// </remarks>
internal static class PatientServiceCreateExtensions
{
    public static Task<PatientResponse> CreateAsync(
        this PatientService service,
        CreatePatientRequest request,
        CancellationToken cancellationToken) =>
        service.CreateAsync(request, confirmDuplicate: false, cancellationToken);
}
