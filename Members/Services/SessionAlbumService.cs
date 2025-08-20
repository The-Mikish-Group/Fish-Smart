using Members.Data;
using Members.Models;
using Microsoft.EntityFrameworkCore;

namespace Members.Services
{
    public interface ISessionAlbumService
    {
        /// <summary>
        /// Creates an album automatically for a fishing session
        /// </summary>
        /// <param name="session">The fishing session to create an album for</param>
        /// <returns>The created album</returns>
        Task<CatchAlbum> CreateSessionAlbumAsync(FishingSession session);

        /// <summary>
        /// Automatically adds a catch to its session's album
        /// </summary>
        /// <param name="catch">The catch to add to the session album</param>
        /// <returns>True if successfully added</returns>
        Task<bool> AddCatchToSessionAlbumAsync(Catch catchRecord);

        /// <summary>
        /// Gets the album associated with a fishing session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>The session album or null if not found</returns>
        Task<CatchAlbum?> GetSessionAlbumAsync(int sessionId);

        /// <summary>
        /// Deletes a session album when the session is deleted
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>True if album was deleted</returns>
        Task<bool> DeleteSessionAlbumAsync(int sessionId);
    }

    public class SessionAlbumService : ISessionAlbumService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SessionAlbumService> _logger;

        public SessionAlbumService(ApplicationDbContext context, ILogger<SessionAlbumService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CatchAlbum> CreateSessionAlbumAsync(FishingSession session)
        {
            try
            {
                // Generate album name from location or fallback
                string albumName = !string.IsNullOrEmpty(session.LocationName) 
                    ? session.LocationName 
                    : $"Session {session.SessionDate:MMM dd, yyyy}";

                // Create the session album
                var album = new CatchAlbum
                {
                    Name = albumName,
                    Description = $"Automatically created for fishing session on {session.SessionDate:MMMM dd, yyyy 'at' h:mm tt}",
                    UserId = session.UserId,
                    FishingSessionId = session.Id,
                    IsSessionAlbum = true,
                    IsPublic = false, // Session albums are private by default
                    CreatedAt = DateTime.Now
                };

                _context.CatchAlbums.Add(album);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created session album '{AlbumName}' for session {SessionId}", albumName, session.Id);
                return album;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create session album for session {SessionId}", session.Id);
                throw;
            }
        }

        public async Task<bool> AddCatchToSessionAlbumAsync(Catch catchRecord)
        {
            try
            {
                if (catchRecord.SessionId == 0)
                {
                    _logger.LogWarning("Cannot add catch {CatchId} to session album - no session ID", catchRecord.Id);
                    return false;
                }

                // Find the session album
                var sessionAlbum = await GetSessionAlbumAsync(catchRecord.SessionId);
                if (sessionAlbum == null)
                {
                    _logger.LogWarning("No session album found for session {SessionId}", catchRecord.SessionId);
                    return false;
                }

                // Check if catch is already in the album
                var existingEntry = await _context.AlbumCatches
                    .FirstOrDefaultAsync(ac => ac.AlbumId == sessionAlbum.Id && ac.CatchId == catchRecord.Id);

                if (existingEntry != null)
                {
                    _logger.LogInformation("Catch {CatchId} already in session album {AlbumId}", catchRecord.Id, sessionAlbum.Id);
                    return true;
                }

                // Add catch to session album
                var albumCatch = new AlbumCatches
                {
                    AlbumId = sessionAlbum.Id,
                    CatchId = catchRecord.Id,
                    AddedAt = DateTime.Now
                };

                _context.AlbumCatches.Add(albumCatch);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Added catch {CatchId} to session album {AlbumId}", catchRecord.Id, sessionAlbum.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add catch {CatchId} to session album", catchRecord.Id);
                return false;
            }
        }

        public async Task<CatchAlbum?> GetSessionAlbumAsync(int sessionId)
        {
            return await _context.CatchAlbums
                .FirstOrDefaultAsync(a => a.FishingSessionId == sessionId && a.IsSessionAlbum);
        }

        public async Task<bool> DeleteSessionAlbumAsync(int sessionId)
        {
            try
            {
                var sessionAlbum = await GetSessionAlbumAsync(sessionId);
                if (sessionAlbum == null)
                {
                    _logger.LogInformation("No session album found to delete for session {SessionId}", sessionId);
                    return true; // Not an error if there's no album to delete
                }

                // First, remove all AlbumCatches entries for this album to avoid constraint conflicts
                var albumCatches = await _context.AlbumCatches
                    .Where(ac => ac.AlbumId == sessionAlbum.Id)
                    .ToListAsync();
                
                if (albumCatches.Any())
                {
                    _context.AlbumCatches.RemoveRange(albumCatches);
                    _logger.LogInformation("Removing {Count} album-catch relationships for album {AlbumId}", 
                        albumCatches.Count, sessionAlbum.Id);
                }

                // Then remove the album itself
                _context.CatchAlbums.Remove(sessionAlbum);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted session album {AlbumId} for session {SessionId}", sessionAlbum.Id, sessionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete session album for session {SessionId}", sessionId);
                return false;
            }
        }
    }
}