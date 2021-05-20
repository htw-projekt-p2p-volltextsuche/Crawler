using System.ComponentModel.DataAnnotations;

namespace Crawler.Domain.Entities
{
    /// <summary>
    /// Represents a protocol or collection of protocols by an identifier.
    /// Used to determine whether a raw protocol file or zip of protocols has already been indexed by the system.
    /// </summary>
    public class Pointer
    {
        [Required, Key]
        public string Identifier { get; set; }
    }
}
