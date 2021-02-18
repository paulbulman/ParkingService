﻿namespace Parking.Api.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Business;
    using Business.Data;
    using Json.Reservations;
    using Model;
    using NodaTime;
    using static Json.Calendar.Helpers;

    [Route("[controller]")]
    [ApiController]
    public class ReservationsController : ControllerBase
    {
        private readonly IConfigurationRepository configurationRepository;
        
        private readonly IDateCalculator dateCalculator;
        
        private readonly IReservationRepository reservationRepository;
        
        private readonly IUserRepository userRepository;

        public ReservationsController(
            IConfigurationRepository configurationRepository,
            IDateCalculator dateCalculator,
            IReservationRepository reservationRepository,
            IUserRepository userRepository)
        {
            this.configurationRepository = configurationRepository;
            this.dateCalculator = dateCalculator;
            this.reservationRepository = reservationRepository;
            this.userRepository = userRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync()
        {
            var configuration = await this.configurationRepository.GetConfiguration();

            var activeDates = this.dateCalculator.GetActiveDates();

            var reservations = await this.reservationRepository.GetReservations(activeDates.First(), activeDates.Last());

            var calendarData = activeDates.ToDictionary(
                d => d,
                d => CreateDailyData(d, reservations));

            var calendar = CreateCalendar(calendarData);

            var users = await this.userRepository.GetUsers();

            var reservationsUsers = users
                .OrderBy(u => u.LastName)
                .Select(u => new ReservationsUser(u.UserId, $"{u.FirstName} {u.LastName}"));

            var response = new ReservationsResponse(calendar, configuration.ShortLeadTimeSpaces, reservationsUsers);

            return this.Ok(response);
        }

        private static ReservationsData CreateDailyData(
            LocalDate localDate,
            IReadOnlyCollection<Reservation> reservations)
        {
            var filteredReservations = reservations.Where(r => r.Date == localDate);

            return new ReservationsData(filteredReservations.Select(r => r.UserId));
        }
    }
}
