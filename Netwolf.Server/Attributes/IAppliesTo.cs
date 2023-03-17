using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Attributes
{
    public interface IAppliesTo
    {
        /// <summary>
        /// Type of thing this class is allowed to apply to.
        /// </summary>
        Type AllowedType { get; }

        /// <summary>
        /// Test whether this class can apply to the specified object.
        /// </summary>
        /// <param name="obj">Object to check.</param>
        /// <returns><c>true</c> if <paramref name="obj"/> is the <see cref="AllowedType"/> or a subclass of it.</returns>
        bool CanApply(object obj);
    }
}
