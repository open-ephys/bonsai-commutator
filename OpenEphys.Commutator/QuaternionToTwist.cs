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
                Quaternion? previousQuaternion = null;
                return source.Select(rotation =>
                {
                    double twist = 0;
                    if (rotation.Length() == 0) return 0;

                    //Normalize the quaternion
                    var current = Quaternion.Normalize(rotation);
                    if (previousQuaternion.HasValue)
                    {
                        var last = previousQuaternion.Value;
                        //Calculate the incremental rotation
                        var conjugate = Quaternion.Conjugate(last);
                        var delta =current * conjugate;

                        //Rotate RotationAxis to the last known global coordinates
                        var axis = new Quaternion(RotationAxis, 0);
                        var projection = (last * axis) * conjugate;

                        //Get how much the new rotation is performed through the last axis projected in global coordinates
                        var deltaV = new Vector3(delta.X, delta.Y, delta.Z);
                        var projectionV = new Vector3(projection.X, projection.Y, projection.Z);
                        var dotProduct = Vector3.Dot(deltaV, projectionV);
                        twist = 2 * Math.Atan2(dotProduct, delta.W);
                    }

                    previousQuaternion = current;


                    return double.IsNaN(twist) ? 0 : -twist / (2 * Math.PI);
                });
            });
        }
    }
}
