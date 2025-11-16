using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Implementations;
using Mutagen.Bethesda.Environments;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace MunitionAutoPatcher.Services.Interfaces
{
    public interface IWeaponDataExtractor
    {
        /// <summary>
        /// Extract initial OMOD candidates (ConstructibleObject-based) from the given Mutagen environment.
        /// </summary>
        /// <param name="env">Mutagen environment/adapter</param>
        /// <param name="excluded">Set of excluded plugin filenames</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <returns>List of extracted OmodCandidate</returns>
        Task<List<OmodCandidate>> ExtractAsync(IResourcedMutagenEnvironment env, System.Collections.Generic.HashSet<string> excluded, IProgress<string>? progress = null);
    }
}
