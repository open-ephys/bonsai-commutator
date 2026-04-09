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
        /// Gets or sets the direction vector specifying the axis relative to the headstage where the tether is connected.
        /// </summary>
        /// <remarks>
        /// This vector should point, using the reference frame of the device producing rotation measurements, 
        /// in the direction that the tether exits the headstage. Note that negating this vector will result in
        /// negating the direction of twisting.
        /// </remarks>
        [Category(Definitions.ConfigurationCategory)]
        [TypeConverter(typeof(NumericRecordConverter))]
        [Description("The direction vector specifying the axis relative to the headstage where the tether is connected.")]
        public Vector3 HeadstageAxis { get; set; } = Vector3.UnitZ;


        /// <summary>
        /// Gets or sets the direction vector speciying the axis representing the direction in which the tether is plugged into the commutator
        /// </summary>
        /// <remarks>
        /// This vector should point, using the global reference frame, in the direction that the tether enters the rotating
        /// element of the commutator. For a usual vertical, upright mouting, this would be Z. 
        /// </remarks>
        [Category(Definitions.ConfigurationCategory)]
        [TypeConverter(typeof(NumericRecordConverter))]
        [Description("The direction vector specifying the axis representing the direction in which the tether is plugged into the commutator.")]
        public Vector3 CommutatorAxis { get; set; } = Vector3.UnitZ;

        /// <summary>
        /// Gets or sets the threshold in which the twist is not fully calculated and a fallback is used
        /// </summary>
        /// <remarks>
        /// The twist algorithm has a pole when the cosine of the angle between the tether and commutator axes
        /// approaches zero (i.e.: they are complete opposites). When this happens, a fallback needs to be used
        /// </remarks>
        [Category(Definitions.ConfigurationCategory)]
        [Range(-0.9999,0.5)]
        [Editor(DesignTypes.SliderEditor, DesignTypes.UITypeEditor)]
        [Description("Threshold in which the twist is not fully calculated and a fallback is used.")]
        public double FallbackThreshold { get; set; } = -0.9;

        /// <summary>
        /// Defines the possible fallback modes for when the algorithm reaches the 
        /// mathematical pole defined un <see cref="FallbackThreshold"/>
        /// </summary>
        public enum FallbackRotationModes { 
            /// <summary>
            /// Use the global coordinate axis as rotation origin
            /// </summary>
            /// <remarks>
            /// Fallback mode on the pole will asume rotations are
            /// orbital motions around the commutator
            /// </remarks>
            Global,
            /// <summary>
            /// Use the local coordinate axis as rotation origin
            /// </summary>
            /// <remarks>
            /// Fallback mode on the pole will asume rotations are
            /// rotations around the headstage tether axis
            /// </remarks>
            Local
        }

        /// <summary>
        /// Gets of set the fallback rotation that should be used when the twist algorithm reaches
        /// the threshold set on <see cref="FallbackThreshold"/>
        /// </summary>
        [Category(Definitions.ConfigurationCategory)]
        [Description("Rotation to use when headstage angle reraches the fallback threshold")]
        public FallbackRotationModes FallbackRotation { get; set; } = FallbackRotationModes.Global;

        /// <summary>
        /// Calculates a twist about <see cref="HeadstageAxis"/> 
        /// and <see cref="CommutatorAxis"/>that has occurred between successive rotation 
        /// measurements provided by the input sequence.
        /// </summary>
        /// <param name="source">The sequence of rotation measurements.</param>
        /// <returns>The sequence of twist values, in units of turns.</returns>
        public override IObservable<double> Process(IObservable<Quaternion> source)
        {
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
                        var delta =Quaternion.Normalize(current * conjugate); //normalize to ensure there are no rounding errors

                        //Rotate RotationAxis to the last known global coordinates
                        var localAxis = new Quaternion(HeadstageAxis, 0);
                        var projection = Quaternion.Normalize((last * localAxis) * conjugate);

                        //Get how much the new rotation is performed through the last axis projected in global coordinates
                        var deltaV = new Vector3(delta.X, delta.Y, delta.Z);
                        var projectionV = new Vector3(projection.X, projection.Y, projection.Z);
                        var localDotProduct = Vector3.Dot(deltaV, projectionV);
                        double localTwist = 2 * Math.Atan2(localDotProduct, delta.W);

                        //Get how much of the new rotation is performed through the commutator axis in global coordinates
                        var globalDotProduct = Vector3.Dot(deltaV, CommutatorAxis);
                        var globalTwist = 2*Math.Atan2(globalDotProduct, delta.W);

                        //get the cosine from the rotated axis and the commutator axis
                        //since vectors are normalised, this is just the dot product
                        var cos_angle = Vector3.Dot(projectionV,CommutatorAxis);

                        //Get total angle, correct for the mathematical pole
                        if (cos_angle > FallbackThreshold)
                            twist = (localTwist + globalTwist) / (1.0 + cos_angle);
                        else
                            twist = FallbackRotation == FallbackRotationModes.Global ? globalTwist : localTwist;

                    }

                    previousQuaternion = current;


                    return double.IsNaN(twist) ? 0 : -twist / (2 * Math.PI);
                });
            });
        }
    }
}
