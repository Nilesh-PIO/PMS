using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PMS.Application.Abstractions;

namespace PMS.Api.Filters;

/// <summary>
/// Refuses the action unless first-run clinic setup is complete (E-1, plan F-3 acceptance
/// criterion 3: <c>POST /api/prescriptions/...</c> returns 409 naming setup as incomplete).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is F-3's half of a seam F-14 completes.</b> The prescription endpoints do not exist
/// yet - they are F-14's - so what F-3 ships is the gate itself, ready to be applied. F-14 puts
/// <c>[RequiresSetupComplete]</c> on <c>PrescriptionsController</c> and inherits a tested 409
/// instead of writing its own check.
/// </para>
/// <para>
/// Built as a filter rather than a line inside each action deliberately: a check you have to
/// remember to write is a check that will eventually be forgotten in exactly the place it
/// mattered. An attribute is visible at the top of the controller and greppable across the
/// solution. The service-level <c>EnsureSetupCompleteAsync</c> remains the real enforcement point
/// for any non-HTTP caller.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresSetupCompleteAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new RequiresSetupCompleteFilter(
            serviceProvider.GetRequiredService<IClinicProfileService>());
}

/// <summary>The filter <see cref="RequiresSetupCompleteAttribute"/> creates.</summary>
public sealed class RequiresSetupCompleteFilter : IAsyncActionFilter
{
    private readonly IClinicProfileService _clinicProfile;

    public RequiresSetupCompleteFilter(IClinicProfileService clinicProfile)
    {
        _clinicProfile = clinicProfile;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        // Throws DomainRuleException("setup-incomplete"), which F-1's ProblemDetailsMiddleware
        // turns into a 409 carrying that slug - one error contract, no bespoke response here.
        await _clinicProfile.EnsureSetupCompleteAsync(context.HttpContext.RequestAborted);

        await next();
    }
}
