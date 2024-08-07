using Hik.Web.Pages.Shared;
using Hik.Web.Queries.DashboardDetails;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace Hik.Web.Pages
{
    public class DashboardDetailsModel : PageModel
    {
        private readonly IMediator mediator;

        public DashboardDetailsModel(IMediator mediator)
        {
            this.mediator = mediator;
        }

        public DashboardDetailsDto Dto { get; set; }
        public PagerControl Pager { get; set; }
        public async Task<IActionResult> OnGetAsync(int triggerId, int p = 1, int pageSize = 48)
        {
            if (triggerId <= 0) { return NotFound(); }
            Dto = await mediator.Send(new DashboardDetailsQuery { JobTriggerId = triggerId, CurrentPage = p, PageSize = pageSize}) as DashboardDetailsDto;

            Pager = new(triggerId, "?triggerId=", Dto.TotalItems, p, pageSize);
            return Page();
        }
    }
}