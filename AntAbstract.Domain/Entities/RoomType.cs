using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AntAbstract.Domain.Entities;

namespace AntAbstract.Domain.Entities
{
    public class RoomType : BaseEntity
    {
        [Required]
        public string Name { get; set; } 

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } 

        public string Currency { get; set; } = "TL";

        public int Capacity { get; set; } = 1; 

        public int TotalQuota { get; set; } = 100; 


        public Guid HotelId { get; set; }
        [ForeignKey("HotelId")]
        public Hotel Hotel { get; set; }
    }
}