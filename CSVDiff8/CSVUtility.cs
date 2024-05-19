public class CSVUtility
{
    public static bool CompareCSVString(string x, string y)
    {
        bool boolX = string.IsNullOrEmpty(x);
        bool boolY = string.IsNullOrEmpty(y);
        if ( boolX == true )
        {
            if ( boolY == true )
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            if ( boolY == true )
            {
                return false;
            }
            else
            {
                return x.Equals(y);
/*
//                if (x == y)
                if (System.Text.Encoding.UTF8.GetBytes(x).Equals(System.Text.Encoding.UTF8.GetBytes(y)))
                {
                    return true;
                }
                return false;
*/
            }
        }
    }

    public class CompareResult
    {
        public bool IsMatch {get; set;}
        public List<string> updateKeys {get; set;} = new List<string>();
    }

    public static CompareResult CompareDictionaryUpdate(IEnumerable<string> compareKeys, Dictionary<string, string> x, Dictionary<string, string> y)
    {
        CompareResult retResult = new CompareResult();
        retResult.IsMatch = true;
        retResult.updateKeys = new List<string>();

        foreach (var key in compareKeys)
        {
            if (CompareCSVString(x[key], y[key]) == false)
            {
                retResult.IsMatch = false;
                retResult.updateKeys.Add(key);
            }
            else
            {
            }
        }

        return retResult;
    }
}