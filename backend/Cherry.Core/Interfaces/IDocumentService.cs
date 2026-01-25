using System;
using System.Threading.Tasks;

namespace Cherry.Core.Interfaces
{
    public interface IDocumentService
    {
        Task GeneratePptxAsync(Guid proposalVersionId);
        Task ConvertPptxToPdfAsync(Guid artifactId);
    }
}
