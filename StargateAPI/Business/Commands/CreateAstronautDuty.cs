using Azure.Core;
using Dapper;
using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;
using System.Net;

namespace StargateAPI.Business.Commands
{
    public static class Constants
    {
        public const string RETIRED_TITLE = "RETIRED";
    }

    public class CreateAstronautDuty : IRequest<CreateAstronautDutyResult>
    {
        public required string Name { get; set; }

        public required string Rank { get; set; }

        public required string DutyTitle { get; set; }

        public DateTime DutyStartDate { get; set; }
    }

    public class CreateAstronautDutyPreProcessor : IRequestPreProcessor<CreateAstronautDuty>
    {
        private readonly StargateContext _context;

        public CreateAstronautDutyPreProcessor(StargateContext context)
        {
            _context = context;
        }

        public Task Process(CreateAstronautDuty request, CancellationToken cancellationToken)
        {
            var person = _context.People.AsNoTracking().FirstOrDefault(z => z.Name == request.Name);

            if (person is null) throw new BadHttpRequestException("Bad Request");

            // TODO: review semantics here...

            // (1) The new duty for this person must always have a newer start date, in addition to
            // forbidding a duplicate title + start-date.
            var verifyNoPreviousDuty = _context.AstronautDuties.FirstOrDefault(z =>
                z.PersonId == person.Id && (
                (z.DutyTitle == request.DutyTitle && z.DutyStartDate == request.DutyStartDate) ||
                    z.DutyStartDate >= request.DutyStartDate
                ));

            if (verifyNoPreviousDuty is not null) throw new BadHttpRequestException("Invalid title and/or start-date");

            // (2) Person must be on active current duty in order to be put into retirement
            var astronautDetail = _context.AstronautDetails.AsNoTracking().SingleOrDefault(z => z.PersonId == person.Id);
            if (astronautDetail is null && request.DutyTitle == Constants.RETIRED_TITLE)
            {
                throw new BadHttpRequestException("Person is not on active duty");
            }

            return Task.CompletedTask;
        }
    }

    public class CreateAstronautDutyHandler : IRequestHandler<CreateAstronautDuty, CreateAstronautDutyResult>
    {
        private readonly StargateContext _context;

        public CreateAstronautDutyHandler(StargateContext context)
        {
            _context = context;
        }

        public async Task<CreateAstronautDutyResult> Handle(CreateAstronautDuty request, CancellationToken cancellationToken)
        {

            var person = _context.People.AsNoTracking().Single(z => z.Name == request.Name);

            var astronautDetail = _context.AstronautDetails.AsNoTracking().SingleOrDefault(z => z.PersonId == person.Id);

            if (astronautDetail is null)
            {
                astronautDetail = new AstronautDetail()
                {
                    PersonId = person.Id,
                    CareerStartDate = request.DutyStartDate.Date
                };
                AssignDetails(astronautDetail, request);

                await _context.AstronautDetails.AddAsync(astronautDetail);
            }
            else
            {
                AssignDetails(astronautDetail, request);

                _context.AstronautDetails.Update(astronautDetail);
            }

            var astronautDuty = await _context.AstronautDuties
                .OrderByDescending(d => d.DutyStartDate)
                .FirstOrDefaultAsync(d => d.PersonId == person.Id, cancellationToken: cancellationToken);

            if (astronautDuty is not null)
            {
                // Deactivate the current duty
                astronautDuty.DutyEndDate = request.DutyStartDate.AddDays(-1).Date;
                _context.AstronautDuties.Update(astronautDuty);
            }

            var newAstronautDuty = new AstronautDuty()
            {
                PersonId = person.Id,
                Rank = request.Rank,
                DutyTitle = request.DutyTitle,
                DutyStartDate = request.DutyStartDate.Date,
                DutyEndDate = null
            };

            await _context.AstronautDuties.AddAsync(newAstronautDuty, cancellationToken);

            await _context.SaveChangesAsync();

            return new CreateAstronautDutyResult(newAstronautDuty.Id);
        }

        private static void AssignDetails(AstronautDetail astronautDetail, CreateAstronautDuty duty)
        {
            astronautDetail.CurrentDutyTitle = duty.DutyTitle;
            astronautDetail.CurrentRank = duty.Rank;
            if (duty.DutyTitle == Constants.RETIRED_TITLE)
            {
                // Assertion: we are updating the person's existing details (c.f. preprocess logic above)
                astronautDetail.CareerEndDate = duty.DutyStartDate.AddDays(-1).Date;
            }
        }
    }

    public class CreateAstronautDutyResult : BaseResponse
    {
        public CreateAstronautDutyResult(int id)
        {
            Id = id;
            ResponseCode = (int)HttpStatusCode.Created;
        }

        public int Id { get; set; }
    }
}
