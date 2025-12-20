using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AntAbstract.Domain.Entities;


namespace AntAbstract.Domain.Entities
{
    public class Hotel : BaseEntity
    {
        [Required]
        public string Name { get; set; } 

        public string? Description { get; set; }

        public string? Address { get; set; }

        public string? PhotoPath { get; set; } 


        public Guid ConferenceId { get; set; }
        [ForeignKey("ConferenceId")]
        public Conference Conference { get; set; }


        public ICollection<RoomType> RoomTypes { get; set; }
    }
}