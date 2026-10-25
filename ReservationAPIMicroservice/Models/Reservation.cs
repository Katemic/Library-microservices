namespace ReservationAPIMicroservice.Models
{
    public class Reservation
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public DateTime ReservationTime { get; set; }

        public ReservationStatus Status { get; set; }

        public int QueueNumber { get; set; }
    }
}
