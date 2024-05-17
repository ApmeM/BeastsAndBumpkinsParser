namespace BeastsAndBumpkinsParser
{
    public static class Utils
    {

        // https://stackoverflow.com/a/1403542
        public static string TrimFromZero(string input)
        {
            var index = input.IndexOf('\0');
            if (index < 0)
                return input;

            return input.Substring(0, index);
        }

        public static byte[] UnpackRLE0(byte[] input, int size)
        {
            var output = new byte[size];
            var inPos = 0u;
            var outPos = 0u;

            while (inPos < input.Length)
            {
                var temp = input[inPos++];
                if (temp != 0)
                {
                    output[outPos] = temp;
                    outPos++;
                }
                else
                {
                    int length = input[inPos++];
                    for (var t = 0; t < length; t++)
                    {
                        if (outPos == size) break;
                        output[outPos] = 0;
                        outPos++;
                    }
                }
            }

            return output;
        }

        public static byte[] UnpackRLE(byte[] input, int size)
        {
            var output = new byte[size];
            var inPos = 0u;
            var outPos = 0u;

            var copyByte = input[0];
            while (outPos < output.Length)
            {
                if (input[inPos] == copyByte)
                {
                    var count = input[++inPos];
                    ++inPos;

                    while (count-- != 0)
                    {
                        output[outPos++] = copyByte;
                    }
                }
                else
                {
                    output[outPos++] = input[inPos++];
                }
            }
            return output;
        }

    }
}