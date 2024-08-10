using System;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using Bonsai;

namespace OpenEphys.Commutator
{
    /// <summary>
    /// Calculates a feedback control signal to compensate for twisting about the specified axis,
    /// in units of turns.
    /// </summary>
    [Description("Calculates a feedback control signal to compensate for twisting about the specified axis, in units of turns.")]
    public class QuaternionTwistController : Combinator<Quaternion, double>
    {
        /// <summary>
        /// Gets or sets the direction vector specifying the axis around which to calculate the twist feedback.
        /// </summary>
        [TypeConverter(typeof(NumericRecordConverter))]
        [Description("The direction vector specifying the axis around which to calculate the twist feedback.")]
        public Vector3 RotationAxis { get; set; } = Vector3.UnitZ;

        /// <summary>
        /// Calculates a feedback control signal from an observable sequence of rotation measurements
        /// to compensate for twisting about the specified axis.
        /// </summary>
        /// <param name="source">The sequence of rotation measurements.</param>
        /// <returns>The sequence of controller feedback values, in units of turns.</returns>
        public override IObservable<double> Process(IObservable<Quaternion> source)
        {
            return Observable.Defer(() =>
            {
                double? previousTwist = default;
                return source.Select(rotation =>
                {
                    // project rotation axis onto the direction axis
                    var direction = RotationAxis;
                    var rotationAxis = new Vector3(rotation.X, rotation.Y, rotation.Z);
                    var dotProduct = Vector3.Dot(rotationAxis, direction);
                    var projection = dotProduct / Vector3.Dot(direction, direction) * direction;
                    var twist = new Quaternion(projection, rotation.W);
                    twist = Quaternion.Normalize(twist);
                    if (dotProduct < 0) // account for angle-axis flipping
                    {
                        twist = -twist;
                    }

                    // normalize twist feedback in units of turns
                    var twistAngle = 2 * Math.Acos(twist.W);
                    var feedback = previousTwist.HasValue
                        ? (twistAngle - previousTwist.GetValueOrDefault() + 3 * Math.PI) % (2 * Math.PI) - Math.PI
                        : 0;
                    previousTwist = twistAngle;
                    return -feedback / (2 * Math.PI);
                });
            });
        }
    }
}
