using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;

namespace Veerangana.YouTubeAnalytics.Services
{
    public interface IYouTubeCredentialProvider
    {
        Task<UserCredential> GetCredentialAsync();
    }
}
