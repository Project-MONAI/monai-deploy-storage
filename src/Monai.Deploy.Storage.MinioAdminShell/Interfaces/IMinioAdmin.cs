using Amazon.SecurityToken.Model;
using Monai.Deploy.Storage.Core.Policies;
using Monai.Deploy.Storage.MinioAdmin.Models;

namespace Monai.Deploy.Storage.MinioAdmin.Interfaces
{
    public interface IMinioAdmin
    {
        Credentials CreateReadOnlyUser(string username, PolicyRequest[] policyRequests);
        bool HasConnection();
        void RemoveUser(string username);
        bool SetConnection();
        bool SetPolicy(MinioPolicyType policyType, List<string> policies, string itemName);
        bool UserAlreadyExists(string username);
    }
}
