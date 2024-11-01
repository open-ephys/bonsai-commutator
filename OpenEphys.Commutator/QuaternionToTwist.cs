using System;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using Bonsai;

namespace OpenEphys.Commutator
{
    /// <summary>
    /// Calculates a the rotation about a specified axis (the "twist") that has occurred between successive 3D 
    /// rotation measurements.
    /// </summary>
    [Description("Calculates a the rotation about a specified axis (the \"twist\") that has " +
        "occurred between successive 3D rotation measurements.")]
    public class QuaternionToTwist : Combinator<Quaternion, double>
    {
        /// <summary>
        /// Gets or sets the direction vector specifying the axis around which to calculate the twist.
        /// </summary>
        /// <remarks>
        /// This vector should point, using the reference frame of the device producing rotation measurements, 
        /// in the direction that the tether exits the headstage. Note that negating this vector will result in
        /// negating the direction of twisting.
        /// </remarks>
        [Category(Definitions.ConfigurationCategory)]
        [TypeConverter(typeof(NumericRecordConverter))]
        [Description("The direction vector specifying the axis around which to calculate the twist.")]
        public Vector3 RotationAxis { get; set; } = Vector3.UnitZ;

        /// <summary>
        /// Calculates a twist about <see cref="RotationAxis"/> that has occurred between successive rotation 
        /// measurements provided by the input sequence.
        /// </summary>
        /// <param name="source">The sequence of rotation measurements.</param>
        /// <returns>The sequence of twist values, in units of turns.</returns>
        public override IObservable<double> Process(IObservable<Quaternion> source)
        {
            var rotationAxis = RotationAxis;

            return Observable.Defer(() =>
            {
                double? previousTwist = default;
                return source.Select(rotation =>
                {
                    // project rotation axis onto the direction axis
                    var vectorPart = new Vector3(rotation.X, rotation.Y, rotation.Z);
                    var dotProduct = Vector3.Dot(vectorPart, rotationAxis);
                    var projection = dotProduct / Vector3.Dot(rotationAxis, rotationAxis) * rotationAxis;
                    var rotationAboutAxis = new Quaternion(projection, rotation.W);
                    rotationAboutAxis = Quaternion.Normalize(rotationAboutAxis);
                    if (dotProduct < 0) // account for angle-axis flipping
                    {
                        rotationAboutAxis = -rotationAboutAxis;
                    }

                    // normalize twist feedback in units of turns
                    var angleAboutAxis = 2 * Math.Acos(rotationAboutAxis.W);
                    var twist = previousTwist.HasValue
                        ? (angleAboutAxis - previousTwist.GetValueOrDefault() + 3 * Math.PI) % (2 * Math.PI) - Math.PI
                        : 0;
                    previousTwist = angleAboutAxis;
                    return -twist / (2 * Math.PI);
                });
            });
        }
    }
}
