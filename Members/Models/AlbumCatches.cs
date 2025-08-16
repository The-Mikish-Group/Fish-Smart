using System.ComponentModel.DataAnnotations.Schema;

namespace Members.Models
{
    public class AlbumCatches
    {
        public int AlbumId { get; set; }
        public int CatchId { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("AlbumId")]
        public virtual CatchAlbum? Album { get; set; }

        [ForeignKey("CatchId")]
        public virtual Catch? Catch { get; set; }
    }
}