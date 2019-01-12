using System;
using System.Text;
using UnityEngine;

partial struct ffloat
{
    const int TestCase = 10;

    public static void TestAll()
    {
        Test_ffloat();
        Test_fVector2();
        Test_fVector3();
        Test_fQuaternion();
    }

    #region Test ffloat
    public static void Test_ffloat()
    {
        Test_ffloatTypeConversion();
        Test_ffloatPi();
        Test_ffloatSqrt();
        Test_ffloatTrigonFunctions();
    }

    public static void Test_ffloatTypeConversion()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("ffloat TypeConversion Test");

        for (int i = 1; i <= 200; i++)
        {
            float origin = (float)i / 100.0f;
            ffloat f = new ffloat(origin);
            sb.AppendLine("origin: " + origin + ", ffloat: " + f + ", ffloat ToFloat: " + f);
        }

        Debug.Log(sb);
    }

    public static void Test_ffloatPi()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("ffloat PI Test");

        sb.AppendLine("PI -> ffloat: " + Pi + ", Math: " + Math.PI + ", Diff: " + (Math.PI - Pi.ToDouble()));
        sb.AppendLine("PI to Degree: " + (Pi * Rad2Deg));

        Debug.Log(sb);
    }

    public static void Test_ffloatSqrt()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("ffloat Sqrt Test");

        for (int i = 2; i < TestCase; i++)
        {
            sb.AppendLine(i + " Sqrt -> ffloat: " + Sqrt(new ffloat(i)) + ", Math: " + Math.Sqrt(i));
        }

        Debug.Log(sb);
    }

    public static void Test_ffloatTrigonFunctions()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("ffloat TrigonFunctions Test");
        const double degreeToRadian = Math.PI / 180.0;

        for (int i = 0; i < 15; i++)
        {
            double radian = i * 15.0 * degreeToRadian;
            sb.AppendLine("Sin " + i * 15 +
                "-> ffloat: " + Sin(new ffloat(radian)) +
                ", Math: " + Math.Sin(radian));
        }

        for (int i = 0; i < 15; i++)
        {
            double radian = i * 15.0 * degreeToRadian;
            sb.AppendLine("Cos " + i * 15 +
                "-> ffloat: " + Cos(new ffloat(radian)) +
                ", Math: " + Math.Cos(radian));
        }

        for (int i = 0; i < 15; i++)
        {
            double radian = i * 15.0 * degreeToRadian;
            sb.AppendLine("Tan " + i * 15 +
                "-> ffloat: " + Tan(new ffloat(radian)) +
                ", Math: " + Math.Tan(radian));
        }

        for (int i = 0; i < 10; i++)
        {
            double radian = i / 10.0;
            sb.AppendLine("Asin " + radian +
                "-> ffloat: " + Asin(new ffloat(radian)) +
                ", Math: " + Math.Asin(radian));
        }

        for (int i = 0; i < 10; i++)
        {
            double radian = i / 10.0;
            sb.AppendLine("Acos " + radian +
                "-> ffloat: " + Acos(new ffloat(radian)) +
                ", Math: " + Math.Acos(radian));
        }

        for (int i = 0; i < 10; i++)
        {
            double radian = i / 10.0;
            sb.AppendLine("Atan " + radian +
                "-> ffloat: " + Atan(new ffloat(radian)) +
                ", Math: " + Math.Atan(radian));
        }

        float[] inputx = new float[TestCase];
        for (int i = 0; i < TestCase; i++)
        {
            inputx[i] = UnityEngine.Random.Range(-10.0f, 10.0f);
        }
        float[] inputy = new float[TestCase];
        for (int i = 0; i < TestCase; i++)
        {
            inputy[i] = UnityEngine.Random.Range(-10.0f, 10.0f);
        }

        for (int i = 0; i < 10; i++)
        {
            sb.AppendLine("Atan2 " +
                "-> ffloat: " + Atan2((ffloat)inputx[i], (ffloat)inputy[i]) +
                ", Math: " + Math.Atan2(inputx[i], inputy[i]));
        }

        Debug.Log(sb);
    }
    #endregion

    #region Test fVector2
    public static void Test_fVector2()
    {
        Test_fVector2Normalize();
        Test_fVector2Rotation();
    }

    public static void Test_fVector2Normalize()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("fVector2 Normalize Test");

        Vector2[] inputs = new Vector2[TestCase];
        for (int i = 0; i < TestCase; i++)
        {
            inputs[i] = new Vector2(UnityEngine.Random.Range(-10.0f, 10.0f), UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        for (int i = 0; i < TestCase; i++)
        {
            sb.AppendLine(inputs[i] +
                " -> fVector2: " + (new fVector2(inputs[i])).normalized +
                ", Vector2: " + inputs[i].normalized);
        }

        Debug.Log(sb);
    }

    public static void Test_fVector2Rotation()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("fVector2 Rotation Test");

        Vector2[] inputs = new Vector2[TestCase];
        for (int i = 0; i < TestCase; i++)
        {
            inputs[i] = new Vector2(UnityEngine.Random.Range(-10.0f, 10.0f), UnityEngine.Random.Range(-10.0f, 10.0f));
        }
        float[] rots = new float[TestCase];
        for (int i = 0; i < TestCase; i++)
            rots[i] = UnityEngine.Random.Range(-320.0f, 320.0f);

        for (int i = 0; i < inputs.Length; i++)
        {
            fVector2 rotfVec = new fVector2(inputs[i]);
            rotfVec.Rotate((ffloat)rots[i]);

            sb.AppendLine(inputs[i] + "&" + rots[i] +
                " Rotation -> fVector2: " + rotfVec);
        }

        Debug.Log(sb);
    }

    #endregion

    #region Test fVector3
    public static void Test_fVector3()
    {
        Test_fVector3Normalize();
        Test_fVector3Distance();
        Test_fVector3Rotation();
        Test_fVector3Angle();
        Test_fVector3Dot();
        Test_fVector3Cross();
    }

    public static void Test_fVector3Normalize()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("fVector3 Normalize Test");

        Vector3[] inputs = new Vector3[TestCase];
        for (int i = 0; i < inputs.Length; i++)
        {
            inputs[i] = new UnityEngine.Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        for (int i = 0; i < inputs.Length; i++)
        {
            sb.AppendLine(inputs[i] +
                " -> fVector3: " + (new fVector3(inputs[i])).normalized +
                ", Vector3: " + inputs[i].normalized);
        }

        Debug.Log(sb);
    }

    public static void Test_fVector3Distance()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("fVector3 Distance Test");

        Vector3[] fromInputs = new Vector3[TestCase];
        for (int i = 0; i < fromInputs.Length; i++)
        {
            fromInputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        Vector3[] toInputs = new Vector3[TestCase];
        for (int i = 0; i < toInputs.Length; i++)
        {
            toInputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        for (int i = 0; i < fromInputs.Length; i++)
        {
            sb.AppendLine(fromInputs[i] + "&" + toInputs[i] +
                " Distance -> fVector3: " + fVector3.Distance(new fVector3(fromInputs[i]), (new fVector3(toInputs[i]))) +
                ", Vector3: " + Vector3.Distance(fromInputs[i], toInputs[i]));
        }

        Debug.Log(sb);
    }

    public static void Test_fVector3Rotation()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("fVector3 Rotation Test");

        Vector3[] inputs = new Vector3[TestCase];
        for (int i = 0; i < inputs.Length; i++)
        {
            inputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        Vector3[] rotInputs = new Vector3[TestCase];
        for (int i = 0; i < rotInputs.Length; i++)
        {
            rotInputs[i] = new Vector3(
                UnityEngine.Random.Range(-150.0f, 150.0f),
                UnityEngine.Random.Range(-150.0f, 150.0f),
                UnityEngine.Random.Range(-150.0f, 150.0f));
        }

        Quaternion[] rots = new Quaternion[TestCase];
        for (int i = 0; i < inputs.Length; i++)
        {
            rots[i] = Quaternion.Euler(rotInputs[i].x, rotInputs[i].y, rotInputs[i].z);
        }

        for (int i = 0; i < inputs.Length; i++)
        {
            sb.AppendLine(inputs[i] +
                " Rotation -> fVector3: " + (new fQuaternion(rots[i])) * (new fVector3(inputs[i])) +
                ", Vector3: " + rots[i] * inputs[i]);
        }

        Debug.Log(sb);
    }

    public static void Test_fVector3Angle()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("fVector3 Angle Test");

        Vector3[] fromInputs = new Vector3[TestCase];
        for (int i = 0; i < fromInputs.Length; i++)
        {
            fromInputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        Vector3[] toInputs = new Vector3[TestCase];
        for (int i = 0; i < toInputs.Length; i++)
        {
            toInputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        for (int i = 0; i < fromInputs.Length; i++)
        {
            sb.AppendLine(fromInputs[i] + "&" + toInputs[i] +
                " Angle -> fVector3: " + fVector3.Angle(new fVector3(fromInputs[i]), (new fVector3(toInputs[i]))) +
                ", Vector3: " + Vector3.Angle(fromInputs[i], toInputs[i]));
        }

        Debug.Log(sb);
    }

    public static void Test_fVector3Dot()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("fVector3 Dot Test");

        Vector3[] fromInputs = new Vector3[TestCase];
        for (int i = 0; i < fromInputs.Length; i++)
        {
            fromInputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        Vector3[] toInputs = new Vector3[TestCase];
        for (int i = 0; i < toInputs.Length; i++)
        {
            toInputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        for (int i = 0; i < fromInputs.Length; i++)
        {
            sb.AppendLine(fromInputs[i] + "&" + toInputs[i] +
                " Dot -> fVector3: " + fVector3.Dot(new fVector3(fromInputs[i]), (new fVector3(toInputs[i]))) +
                ", Vector3: " + Vector3.Dot(fromInputs[i], toInputs[i]));
        }

        Debug.Log(sb);
    }

    public static void Test_fVector3Cross()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("fVector3 Cross Test");

        Vector3[] fromInputs = new Vector3[TestCase];
        for (int i = 0; i < fromInputs.Length; i++)
        {
            fromInputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        Vector3[] toInputs = new Vector3[TestCase];
        for (int i = 0; i < toInputs.Length; i++)
        {
            toInputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        for (int i = 0; i < fromInputs.Length; i++)
        {
            sb.AppendLine(fromInputs[i] + "&" + toInputs[i] +
                " Cross -> fVector3: " + fVector3.Cross(new fVector3(fromInputs[i]), (new fVector3(toInputs[i]))) +
                ", Vector3: " + Vector3.Cross(fromInputs[i], toInputs[i]));
        }

        Debug.Log(sb);
    }
    #endregion

    #region Test fQuaternion
    public static void Test_fQuaternion()
    {
        Test_fQuaternionEuler();
        Test_fQuaternionLookRotation();
        Test_fQuaternionFromToRotation();
        Test_fQuaternionAngle();
        Test_fQuaternionSlerp();
    }

    public static void Test_fQuaternionEuler()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("fQuaternion Euler Test");

        Vector3[] inputs = new Vector3[TestCase];
        for (int i = 0; i < inputs.Length; i++)
        {
            inputs[i] = new Vector3(
                UnityEngine.Random.Range(-180.0f, 180.0f),
                UnityEngine.Random.Range(-180.0f, 180.0f),
                UnityEngine.Random.Range(-180.0f, 180.0f));
        }

        for (int i = 0; i < TestCase; i++)
        {
            fQuaternion frot = fQuaternion.Euler(inputs[i].x, inputs[i].y, inputs[i].z);
            Quaternion rot = Quaternion.Euler(inputs[i].x, inputs[i].y, inputs[i].z);
            sb.AppendLine(inputs[i] +
                " FromEuler -> fQuaternion: " + frot +
                ", Quaternion: " + rot);
            sb.AppendLine(inputs[i] +
                " ToEuler -> fQuaternion: " + frot.eulerAngles +
                ", Quaternion: " + rot.eulerAngles);
            sb.AppendLine(inputs[i] +
                " Restore -> fQuaternion: " + fQuaternion.Euler(frot.eulerAngles) +
                ", Quaternion: " + Quaternion.Euler(rot.eulerAngles));
        }

        Debug.Log(sb);
    }

    public static void Test_fQuaternionLookRotation()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("fQuaternion LookRotation Test");

        Vector3[] inputs = new Vector3[TestCase];
        for (int i = 0; i < inputs.Length; i++)
        {
            inputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        for (int i = 0; i < TestCase; i++)
        {
            sb.AppendLine(inputs[i] +
                " LookRotation -> fQuaternion: " + fQuaternion.LookRotation(new fVector3(inputs[i])) +
                ", Quaternion: " + Quaternion.LookRotation(inputs[i]));
        }

        Debug.Log(sb);
    }

    public static void Test_fQuaternionFromToRotation()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("fQuaternion FromToRotation Test");

        Vector3[] fromInputs = new Vector3[TestCase];
        for (int i = 0; i < fromInputs.Length; i++)
        {
            fromInputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
            fromInputs[i].Normalize();
        }

        Vector3[] toInputs = new Vector3[TestCase];
        for (int i = 0; i < toInputs.Length; i++)
        {
            toInputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
            toInputs[i].Normalize();
        }

        for (int i = 0; i < fromInputs.Length; i++)
        {
            fVector3 from = new fVector3(fromInputs[i]);
            fVector3 to = new fVector3(toInputs[i]);
            fQuaternion fromto = fQuaternion.FromToRotation(from, to);
            Quaternion fromtou = Quaternion.FromToRotation(fromInputs[i], toInputs[i]);
            sb.AppendLine(fromInputs[i] + "&" + toInputs[i] +
                " FromTo -> fQuaternion: " + fromto + "[" + fromto * from + "]" +
                ", Quaternion: " + fromtou);
        }

        Debug.Log(sb);
    }

    public static void Test_fQuaternionAngle()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("fQuaternion Angle Test");

        Vector3[] fromInputs = new Vector3[TestCase];
        for (int i = 0; i < fromInputs.Length; i++)
        {
            fromInputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        Vector3[] toInputs = new Vector3[TestCase];
        for (int i = 0; i < toInputs.Length; i++)
        {
            toInputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        for (int i = 0; i < fromInputs.Length; i++)
        {
            sb.AppendLine(fromInputs[i] + "&" + toInputs[i] +
                " Angle -> fQuaternion: " + fQuaternion.Angle(fQuaternion.Euler(new fVector3(fromInputs[i])), fQuaternion.Euler(new fVector3(toInputs[i]))) +
                ", Quaternion: " + Quaternion.Angle(Quaternion.Euler(fromInputs[i]), Quaternion.Euler(toInputs[i])));
        }

        Debug.Log(sb);
    }

    public static void Test_fQuaternionSlerp()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("fQuaternion Slerp Test");

        Vector3[] fromInputs = new Vector3[TestCase];
        for (int i = 0; i < fromInputs.Length; i++)
        {
            fromInputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        Vector3[] toInputs = new Vector3[TestCase];
        for (int i = 0; i < toInputs.Length; i++)
        {
            toInputs[i] = new Vector3(
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f),
                UnityEngine.Random.Range(-10.0f, 10.0f));
        }

        float[] rate = new float[TestCase];
        for (int i = 0; i < TestCase; i++)
        {
            rate[i] = UnityEngine.Random.Range(0.0f, 1.0f);
        }

        for (int i = 0; i < fromInputs.Length; i++)
        {
            sb.AppendLine(fromInputs[i] + "&" + toInputs[i] +
                " Slerp -> fQuaternion: " + fQuaternion.Slerp(fQuaternion.Euler(new fVector3(fromInputs[i])), fQuaternion.Euler(new fVector3(toInputs[i])), (ffloat)rate[i]) +
                ", Quaternion: " + Quaternion.Slerp(Quaternion.Euler(fromInputs[i]), Quaternion.Euler(toInputs[i]), rate[i]));
        }

        Debug.Log(sb);
    }
    #endregion
}
