﻿using Hik.Helpers;
using Hik.DTO.Contracts;
using Hik.Web.Queries.Dashboard;
using Hik.Web.Queries.QuartzTriggers;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Hik.Web.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly IMediator mediator;

        public DashboardModel(IMediator mediator)
        {
            this.mediator = mediator;
            JobTriggers = new();
        }

        public DateTime Day { get; set; }

        public DashboardDto Dto { get; private set; }
        public Dictionary<string, IList<TriggerDto>> JobTriggers { get; }
        public async Task<IActionResult> OnGet(DateTime? day = null)
        {
            Day = day ?? DateTime.Today;
            this.Dto = await mediator.Send(new DashboardQuery() { Day = this.Day }) as DashboardDto;

            var cronTriggers = await mediator.Send(new QuartzTriggersQuery()) as QuartzTriggersDto;
            foreach (var item in cronTriggers.Triggers)
            {
                var tri = Dto.Triggers.FirstOrDefault(x => x.Name == item.Name && x.Group == item.Group);
                JobTriggers.SafeAdd(item.ClassName, tri);
            }

            return Page();
        }
    }
}