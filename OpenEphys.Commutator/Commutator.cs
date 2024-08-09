using System;
using System.ComponentModel;
using System.Numerics;
using System.Reactive.Linq;
using Bonsai;
using Bonsai.IO.Ports;
using Bonsai.Reactive;

namespace OpenEphys.Commutator
{
    /// <summary>
    /// Controls an Open Ephys commutator using orientation measurements.
    /// </summary>
    [Description("Turns an Open Ephys commutator by the amount specified.")]
    public class Commutator : Combinator<Quaternion, string>
    {
        /// <summary>
        /// Gets or sets the name of the serial port that the commutator is plugged into.
        /// </summary>
        [TypeConverter("Bonsai.IO.Ports.PortNameConverter, Bonsai.System")]
        [Description("The name of the serial port that the commutator is plugged into..")]
        public string PortName { get; set; }

        /// <summary>
        /// Gets or sets the direction vector specifying the axis around which to calculate rotations
        /// </summary>
        /// <remarks>
        /// This direction should be parallel to the major axis of the tether in order to compensate for twisting.
        /// </remarks>
        [TypeConverter(typeof(NumericRecordConverter))]
        [Description("The direction vector specifying the axis around which to calculate rotations.")]
        public Vector3 RotationAxis { get; set; } = Vector3.UnitZ;

        /// <summary>
        /// Sends commands to an Open Ephys commutator using orientation measurements information. 
        /// </summary>
        /// <param name="source">Sequence of orientation measurements.</param>
        /// <returns>Sequence of commands sent to the commutator.</returns>
        public override IObservable<string> Process(IObservable<Quaternion> source)
        {
            double? last = null;
            var gate = new SampleInterval() { Interval = new(0, 0, 0, 0, 100) };
            var writer = new SerialWriteLine() { PortName = PortName };

            var quaternionToCommand = gate.Process(source).Select(orientation =>
            {
                var direction = RotationAxis;
                var rotationAxis = new Vector3(orientation.X, orientation.Y, orientation.Z);
                var dotProduct = Vector3.Dot(rotationAxis, direction);
                var projection = dotProduct / Vector3.Dot(direction, direction) * direction;
                var twist = new Quaternion(projection, orientation.W);
                twist = Quaternion.Normalize(twist);
                if (dotProduct < 0) // account for angle-axis flipping
                {
                    twist = -twist;
                }

                var angle = 2 * Math.Acos(twist.W);

                var a1 = angle + 2 * Math.PI;
                var a2 = angle - 2 * Math.PI;
                var pos = new double[] { angle - (last ?? angle), a1 - (last ?? angle), a2 - (last ?? angle) };
                last = angle;
                var index = 0;
                var currMax = Math.PI;
                for (int count = 0; count < pos.Length; count++)
                {
                    if (Math.Abs(pos[count]) < currMax)
                    {
                        currMax = pos[count];
                        index = count;
                    }

                }

                var turns = pos[index] / (2 * Math.PI);
                turns = double.IsNaN(turns) || double.IsInfinity(turns) ? 0 : turns;
                return  $"{{turn: {turns}}}";
            });


            return writer.Process(quaternionToCommand);

        }
    }
}
