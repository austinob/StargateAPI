using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;
using System.Net;

namespace StargateAPI.Business.Commands
{
    public class ModifyPerson : IRequest<ModifyPersonResult>
    {
        public required string OldName { get; set; }
        public required string NewName { get; set; }
    }

    public class ModifyPersonPreProcessor : IRequestPreProcessor<ModifyPerson>
    {
        private readonly StargateContext _context;

        public ModifyPersonPreProcessor(StargateContext context)
        {
            _context = context;
        }

        public Task Process(ModifyPerson request, CancellationToken cancellationToken)
        {
            var person = _context.People.AsNoTracking().SingleOrDefault(z => z.Name == request.OldName);

            if (person is null) throw new BadHttpRequestException("Person does not exist", (int)HttpStatusCode.NotFound);

            return Task.CompletedTask;
        }
    }

    public class ModifyPersonHandler : IRequestHandler<ModifyPerson, ModifyPersonResult>
    {
        private readonly StargateContext _context;

        public ModifyPersonHandler(StargateContext context)
        {
            _context = context;
        }

        public async Task<ModifyPersonResult> Handle(ModifyPerson request, CancellationToken cancellationToken)
        {
            var existingPerson = _context.People.Single(z => z.Name == request.OldName);
            existingPerson.Name = request.NewName;

            _context.People.Update(existingPerson);

            await _context.SaveChangesAsync(cancellationToken);

            return new ModifyPersonResult()
            {
                Id = existingPerson.Id
            };
        }
    }

    public class ModifyPersonResult : BaseResponse
    {
        public int Id { get; set; }
    }
}
