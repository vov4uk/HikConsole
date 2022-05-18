﻿using Hik.DataAccess.Abstractions;
using Hik.DataAccess.Data;
using Microsoft.EntityFrameworkCore;

namespace Hik.Web.Queries.FilePath
{
    public class FilePathQueryHandler : QueryHandler<FilePathQuery>
    {
        private readonly IUnitOfWorkFactory factory;

        public FilePathQueryHandler(IUnitOfWorkFactory factory)
        {
            this.factory = factory;
        }

        protected override async Task<IHandlerResult> HandleAsync(FilePathQuery request, CancellationToken cancellationToken)
        {
            using (var uow = factory.CreateUnitOfWork(QueryTrackingBehavior.NoTracking))
            {
                IBaseRepository<MediaFile> filesRepo = uow.GetRepository<MediaFile>();

                var file = await filesRepo.FindByAsync(x => x.Id == request.FileId);

                return new FilePathDto
                {
                    Id = request.FileId,
                    Path = file.GetPath(),
                };
            }
        }
    }
}