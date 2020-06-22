using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Windows.Forms;

namespace UartCommunication
{
    public static class UartCom
    {
        /*Control variables ------------------------------------------------------------------------------------------------------------------------*/
        public static bool rxack = true;
        public static bool rxnak = false;
        public static bool rxack_16byte = true;
        public static bool rxnak_16byte = true;
        public static byte[] txbuf = new byte[5];
        public static byte[] rxbuf = new byte[16];

        /*Instruction enums ------------------------------------------------------------------------------------------------------------------------*/
        public enum DataHeader
        {
            Measure = 0x03,
            Data = 0x04, Floattype = 0x05,
            Run = 0x11, Stop = 0x12, Velocity = 0x13, Position = 0x14, Setpoint = 0x15, Realtime = 0x16,
            Kp = 0x17, Ki = 0x18, Kd = 0x19,
            Calib = 0x31,
            Nak = 0xF0, Ack = 0xF1
        };
        public enum ControlHeader
        {
            Run = 0x11, Stop = 0x12, Velocity = 0x13, Position = 0x14, Setpoint = 0x15, Realtime = 0x16,
            Kp = 0x17, Ki = 0x18, Kd = 0x19,
            Calib = 0x31,
            Nak = 0xF0, Ack = 0xF1
        };
        enum FrameHeader { STX = 0xFE, ETX = 0xFF, DLE = 0xFD };

        public static string[] motorMessage =
        {
            "Motor is running...",
            "Motor Stopped!",
            "Velocity Mode",
            "Position Mode",
            "Setpoint Set: ",
            "Kp Set: ",
            "Ki Set: ",
            "Kd Set: ",
            "Calib Set: ",
        };

        /*Handshake methods ------------------------------------------------------------------------------------------------------------------------*/
        public static bool RxHandshake(byte[] inByte, out byte[] instruction)
        {
            byte[] temp = new byte[5];
            if ((FrameHeader)inByte[0] != FrameHeader.STX)
            {
                rxnak = true;
            }
            if ((FrameHeader)inByte[6] != FrameHeader.ETX)
            {
                rxnak = true;
            }
            if (!rxnak)
            {
                for (int ii = 0; ii <= 4; ii++)
                    temp[ii] = inByte[ii + 1];

                if (temp.SequenceEqual(txbuf))
                {
                    rxack = true;
                    instruction = temp;
                    return true;
                }
                else
                {
                    rxnak = true;
                    instruction = null;
                    return false;
                }
            }
            else
            {
                instruction = null;
                return false;
            }

        }
        public static bool RxHandshake_16byte(byte[] inByte, out byte[] instruction)
        {
            byte[] temp = new byte[16];
            if ((FrameHeader)inByte[0] != FrameHeader.STX)
            {
                rxnak_16byte = true;
            }
            if ((FrameHeader)inByte[16] != FrameHeader.ETX)
            {
                rxnak_16byte = true;
            }
            if (!rxnak_16byte)
            {
                for (int ii = 0; ii <= 13; ii++) 
                    temp[ii] = inByte[ii + 1];

                //if (temp.SequenceEqual(txbuf))
                //{
                    rxack_16byte = true;
                    instruction = temp;
                    rxbuf = temp;
                    return true;
                //}
                //else
                //{
                //    rxnak = true;
                //    instruction = null;
                //    return false;
                //}
            }
            else
            {
                instruction = null;
                return false;
            }

        }

        public static bool TxHandshake (ControlHeader header, string text, out byte[] buffer)
        {
            if (rxack)
            {
                txbuf = headerEncapsulation(header, text);
                byte[] temp = new byte[7];
                temp[0] = (byte)FrameHeader.STX;
                for (int i = 1; i <= 5; i++)
                {
                    temp[i] = txbuf[i - 1];
                }
                temp[6] = (byte)FrameHeader.ETX;
                buffer = temp;
                rxack = false;
                return true;
            }
            else
            {
                buffer = null;
                MessageBox.Show("A transmission is ongoing", "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
        public static bool TxHandshake_16byte(out byte[] buffer)
        {
            if (rxack_16byte)
            {
                //txbuf = headerEncapsulation(header, text);
                byte[] temp = new byte[4];
                temp[0] = (byte)FrameHeader.STX;
                //for (int i = 1; i <= 5; i++)
                //{
                //    temp[i] = txbuf[i - 1];
                //}
                temp[1] = (byte)DataHeader.Ack;
                temp[2] = rxbuf[13];
                temp[3] = (byte)FrameHeader.ETX;
                buffer = temp;
                rxack_16byte = false;
                return true;
            }
            else
            {
                //buffer = null;
                //MessageBox.Show("A transmission is ongoing", "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //return false;

                //txbuf = headerEncapsulation(header, text);
                byte[] temp = new byte[4];
                temp[0] = (byte)FrameHeader.STX;
                //for (int i = 1; i <= 5; i++)
                //{
                //    temp[i] = txbuf[i - 1];
                //}
                temp[1] = (byte)DataHeader.Nak;
                temp[2] = rxbuf[13];
                temp[3] = (byte)FrameHeader.ETX;
                buffer = temp;
                rxack_16byte = false;
                return true;
            }
        }

        public static bool ReHandshake (out byte[] buffer)
        {
            if (!rxnak && rxack)
            {
                buffer = null;
                return false;
            }
            else
            {
                rxnak = false;
                rxack = false;
                byte[] temp = new byte[7];
                temp[0] = (byte)FrameHeader.STX;
                for (int i = 1; i <= 5; i++)
                {
                    temp[i] = txbuf[i - 1];
                }
                temp[6] = (byte)FrameHeader.ETX;
                buffer = temp;
                return true;
            }

            
        }

        /*Instruction processing methods ------------------------------------------------------------------------------------------------------------------------*/
        public static byte[] headerEncapsulation(ControlHeader header, string text)
        {
            byte[] temp = new byte[5];
            switch (header)
            {
                case ControlHeader.Realtime:
                    temp[0] = (byte)ControlHeader.Realtime;
                    break;
                case ControlHeader.Setpoint:
                    temp[0] = (byte)ControlHeader.Setpoint;
                    break;
                case ControlHeader.Run:
                    temp[0] = (byte)ControlHeader.Run;
                    break;
                case ControlHeader.Stop:
                    temp[0] = (byte)ControlHeader.Stop;
                    break;
                case ControlHeader.Velocity:
                    temp[0] = (byte)ControlHeader.Velocity;
                    break;
                case ControlHeader.Position:
                    temp[0] = (byte)ControlHeader.Position;
                    break;
                case ControlHeader.Kp:
                    temp[0] = (byte)ControlHeader.Kp;
                    break;
                case ControlHeader.Ki:
                    temp[0] = (byte)ControlHeader.Ki;
                    break;
                case ControlHeader.Kd:
                    temp[0] = (byte)ControlHeader.Kd;
                    break;
                case ControlHeader.Calib:
                    temp[0] = (byte)ControlHeader.Calib;
                    break;
                default:
                    break;
            }
            byte[] tempBuffer = stringtoUART(text);
            temp[1] = tempBuffer[3];
            temp[2] = tempBuffer[2];
            temp[3] = tempBuffer[1];
            temp[4] = tempBuffer[0];
            return temp;
        }

        public static DataHeader classifyHeader(byte[] inData, out byte[] outData)
        {
            outData = new byte[10];

            outData[0] = inData[4];
            outData[1] = inData[3];
            outData[2] = inData[2];
            outData[3] = inData[1];
            return (DataHeader)inData[0];
        }

        /*Data type changing methods ------------------------------------------------------------------------------------------------------------------------*/
        public static byte[] stringtoUART(string text)
        {
            byte[] temp = new byte[4];
            float floattemp = float.Parse(text);
            temp = BitConverter.GetBytes(floattemp);
            return temp;
        }

        public static float uARTBytetoFloat(byte[] uBuffer)
        {
            float value = 0;
            value = BitConverter.ToSingle(uBuffer, 0);
            return value;
        }

    }
}


