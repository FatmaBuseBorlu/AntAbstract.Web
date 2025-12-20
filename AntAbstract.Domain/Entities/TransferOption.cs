using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AntAbstract.Domain.Entities;

namespace AntAbstract.Domain.Entities
{
    public class TransferOption : BaseEntity
    {
        public string Name { get; set; } 

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public string Currency { get; set; } = "TL";

        public Guid ConferenceId { get; set; }
        public Conference Conference { get; set; }
    }
}
