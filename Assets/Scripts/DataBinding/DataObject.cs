using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;


namespace DataBinding
{

    public class DataObject
    {
        float[,] dataArray;
        int dataPoints;
        int nbDimensions;

        float[,] originalDataValues;

        public int NbDimensions
        {
            get { return nbDimensions; }
            set { nbDimensions = value; }
        }

        public int DataPoints
        {
            get { return dataPoints; }
            set { dataPoints = value; }
        }

        public float[,] DataArray
        {
            get { return dataArray; }
            set { dataArray = value; }
        }

        string[] identifiers;

        public string[] Identifiers
        {
            get { return identifiers; }
            set { identifiers = value; }
        }
        string[] typesToRead;

        public struct DimensionMetadata {
            public float minValue;
            public float maxValue;
            public int binCount;
        }

        public DimensionMetadata[] Metadata {
            get { return metadata; }
        }
        DimensionMetadata[] metadata;

        public Dictionary<string, List<float>> sortedDimensions = new Dictionary<string,List<float>>();

        public DataObject(string data, DataObjectMetadata metadata=null)
        {
            loadCSV(data, metadata);
        }

        Dictionary<float, string> textualDimensions = new Dictionary<float, string>();

        /// <summary>
        /// This dictionary contains the values of string values
        /// </summary>
        public Dictionary<float, string> TextualDimensions
        {
            get { return textualDimensions; }
            set { textualDimensions = value; }
        }


        char[] split = new char[] { ',', '\t', ';' };

        Dictionary<int, Vector2> dimensionsRange = new Dictionary<int, Vector2>();
        /// <summary>
        /// This dictionary contains the *original* range values for all dimensions. Value.X is the minimum value of the range, Value.Y the maximum
        /// </summary>
        public Dictionary<int, Vector2> DimensionsRange
        {
            get { return dimensionsRange; }
            set { dimensionsRange = value; }
        }


        Dictionary<int,string> TypeDimensionDictionary = new Dictionary<int,string>();
        /// <summary>
        /// This dictionary contains the type of each dimension (e.g. string, float, bool, int...)
        /// </summary>
        public Dictionary<int,string> TypeDimensionDictionary1
        {
            get { return TypeDimensionDictionary; }
            set { TypeDimensionDictionary = value; }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        public void loadCSV(string data, DataObjectMetadata metadataPreset)
        {
            string[] lines = data.ToString().TrimEnd(Environment.NewLine.ToCharArray()).Split('\n');

            //1: read types
            identifiers = lines[0].Split(split);        
            typesToRead = lines[1].Split(split);

            // create metadata
            metadata = new DimensionMetadata[identifiers.Count()];

            //clean identifiers strings
            for (int i = 0; i < identifiers.Length; i++)
            {
                string id = identifiers[i].Replace("\r", string.Empty);
                identifiers[i] = id;
            }
            nbDimensions = identifiers.Length;

            dataArray = new float[lines.Length-1, identifiers.Length]; // ignore the first line of identifiers
            dataPoints = dataArray.GetUpperBound(0)+1;

            string[] theTypes = new string[typesToRead.Length];

            //type reading
            for (int i = 0; i < typesToRead.Length; i++)
            {
                if (isBool(typesToRead[i]))
                {
                    theTypes[i] = "bool";
                    TypeDimensionDictionary.Add(i, "bool");
                }
                else if (isFloat(typesToRead[i]))
                {
                    theTypes[i] = "float";
                    TypeDimensionDictionary.Add(i, "float");
                }
                else
                {
                    theTypes[i] = "string";
                    TypeDimensionDictionary.Add(i, "string");
                }
            }

            float textualPointer = 0f;
            //line reading
            for (int i = 1; i < lines.Length; i++)
            {
                string[] values = lines[i].Split(split);
                //dimension reading
                for (int k = 0; k < identifiers.Length; k++)
                {

                    string cleanedValue = values[k].Replace("\r", string.Empty);

                    //1- get the corresponding type
                    if (isBool(cleanedValue))
                    {
                        dataArray[i - 1, k] = Convert.ToSingle(bool.Parse(cleanedValue));
                    }
                    else if (cleanedValue.Contains(':'))// isDateTime(values[k]))
                    {
                        //DateTime dt = DateTime.Parse(values[k]);
                        string[] valH = cleanedValue.Split(':');
                        dataArray[i - 1, k] = float.Parse(valH[0]) * 60 + float.Parse(valH[1]);// *60 + float.Parse(valH[2]);
                    }
                    else if (isInt(cleanedValue))
                    {
                        dataArray[i - 1, k] = (float)int.Parse(cleanedValue);
                    }
                    else if (isFloat(cleanedValue))
                    {
                        //Debug.Log(k);
                        dataArray[i - 1, k] = float.Parse(cleanedValue);
                    }
                    else if (!String.IsNullOrEmpty(cleanedValue))
                    {
                        //lookup if already encoded
                        if (textualDimensions.ContainsValue(cleanedValue))
                        {
                            //Debug.Log(i + " " + k);
                            dataArray[i - 1, k] = textualDimensions.FirstOrDefault(x => x.Value == cleanedValue).Key;
                        }
                        else
                        {
                            //new key
                            textualPointer++;
                            textualDimensions.Add(textualPointer, cleanedValue);
                            dataArray[i - 1, k] = textualPointer;
                        }
                    }
                    else
                    {
                        dataArray[i - 1, k] = 0f;
                    }
                }
            }

            normaliseArray(metadataPreset);

            //build the dictionnary of sorted dimensions
            for(int i=0;i<identifiers.Length;i++)
            {
                List<float> sortedDimension = getDimension(identifiers[i]).ToList();
                sortedDimension.Sort();
                sortedDimensions.Add(identifiers[i], sortedDimension);
            }

        }
        
        /// <summary>
        /// internal function: normalises all the data input between 0 and 1
        /// </summary>
        private void normaliseArray(DataObjectMetadata metadataPreset)
        {
            //1 make a copy of the parsed array
            float[,] normArray = new float[dataArray.GetUpperBound(0)+1, dataArray.GetUpperBound(1)+ 1];

            originalDataValues = new float[dataArray.GetUpperBound(0) + 1, dataArray.GetUpperBound(1) + 1];

            //for each dimensions (column) normalise all data
            for (int i = 0; i <= normArray.GetUpperBound(1); i++)
            {
                float[] rawDimension = GetCol(dataArray, i);
                float minDimension = rawDimension.Min();
                float maxDimension = rawDimension.Max();

                metadata[i].minValue = minDimension;
                metadata[i].maxValue = maxDimension;
                metadata[i].binCount = (int)Mathf.Min(maxDimension - minDimension + 1, 200);
                // TODO: add some automated bin size calculations

                if (metadataPreset != null){
                    foreach (var binSizePreset in metadataPreset.BinSizePreset){
                        if (binSizePreset.index == i){
                            metadata[i].binCount = binSizePreset.binCount;
                        }
                    }
                }

                float[] normalisedDimension = new float[rawDimension.Length];

                dimensionsRange.Add(i, new Vector2(minDimension, maxDimension));

                for (int j = 0; j < rawDimension.Length; j++)
                {
                    if (minDimension < maxDimension)
                    {
                        normalisedDimension[j] = normaliseValue(rawDimension[j], minDimension, maxDimension, 0f, 1f);
                    }
                    else
                    {
                        // avoid NaNs or nonsensical normalization
                        normalisedDimension[j] = 0;
                    }
                }

                SetCol<float>(normArray, i, normalisedDimension);

            }
            originalDataValues = dataArray;
            dataArray = normArray;


        }

        public int getNearestDataIndex(float value, int dimension)
        {
            return nearestValue(sortedDimensions[indexToDimension(dimension)], value);
        }

        /// <summary>
        /// gets the original value from the dimension
        /// </summary>
        /// <param name="value"></param>
        /// <param name="dimension"></param>
        /// <returns></returns>
        public object getOriginalValue(float value, int dimension)
        {
            
            int NearestValue = nearestValue(sortedDimensions[indexToDimension(dimension)], value);
            float originalValue = getOriginalDimension(indexToDimension(dimension))[NearestValue];// // ( GetCol(originalDataValues, NearestValue);// normaliseValue(NearestValue, 0f, 1f, dimensionsRange[dimension].x, dimensionsRange[dimension].y);

            //if (TypeDimensionDictionary[dimension] == "string")
            //{
            //    return textualDimensions[originalValue];
            //}
            //else
                return originalValue;
        }

        /// <summary>
        /// debug function that prints the 2D array
        /// </summary>
        /// <param name="data"></param>
        public void Debug2DArray(object[,] data)
        {
            for (int i = 0; i < data.GetUpperBound(0); i++)
            {
                string line = "";
                for (int j = 0; j < data.GetUpperBound(1); j++)
                {
                    line += (data[i, j]) + " ";
                }
                Debug.Log(line);
            }
        }

        /// <summary>
        /// debugs one column
        /// </summary>
        /// <param name="col"></param>
        public void DebugArray(int col)
        {
            float[] selection = getDimension(identifiers[col]);

            for (int i = 0; i < selection.Length; i++)
                Debug.Log(selection[i]);
        }

        /// <summary>
        /// returns one row of 2D array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="matrix"></param>
        /// <param name="row"></param>
        /// <returns></returns>
        public T[] GetRow<T>(T[,] matrix, int row)
        {
            var rowLength = matrix.GetLength(1);
            var rowVector = new T[rowLength];

            for (var i = 0; i < rowLength; i++)
                rowVector[i] = matrix[row, i];

            return rowVector;
        }

        /// <summary>
        /// returns one column of the 2D array
        /// </summary>
        /// <param name="matrix"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        public float[] GetCol(float[,] matrix, int col)
        {
            var colLength = matrix.GetLength(0);
            var colVector = new float[colLength];

            for (var i = 0; i < colLength; i++)
            {
                colVector[i] = matrix[i, col];
            }
            return colVector;
        }

        /// <summary>
        /// sets a vector of values into a specific column
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="matrix"></param>
        /// <param name="col"></param>
        /// <param name="colVector"></param>
        public void SetCol<T>(T[,] matrix, int col, T[] colVector)
        {
            var colLength = matrix.GetLength(0);
            for (var i = 0; i < colLength; i++)
                matrix[i, col] = colVector[i];
        }

        /// <summary>
        /// returns an array of values corresponding to the  
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        public float[] getDimension(int col)
        {
            return getDimension(identifiers[col]);
        }

        public float[] getDimension(string name)
        {

            // 1 bind name to position in array
            int selectCol = -1;
            for (int i = 0; i < identifiers.Length; i++)
            {
                if (identifiers[i] == name)
                    selectCol = i;
            }
            if (selectCol < 0)
                return null;
            else
            {
                return GetCol(dataArray, selectCol);
            }

        }

        public float[] getOriginalDimension(string name)
        {
            // 1 bind name to position in array
            int selectCol = -1;
            for (int i = 0; i < identifiers.Length; i++)
            {
                if (identifiers[i] == name)
                    selectCol = i;
            }
            if (selectCol < 0)
                return null;
            else
            {
                return GetCol(originalDataValues, selectCol);
            }
        }

        public int dimensionToIndex(string dimension)
        {
            int id = -1;
            for (int i = 0; i < identifiers.Length; i++)
            {

                if (dimension == identifiers[i])
                {
                    id = i;
                }
            }
            return id;
        }

        public string indexToDimension(int dimensionIndex)
        {
            return identifiers.ElementAt(dimensionIndex);
        }

        float stringToFloat(string value)
        {
            return BitConverter.ToSingle(Encoding.UTF8.GetBytes(value), 0);
        }

        string floatToString(float value)
        {
            return BitConverter.ToString(BitConverter.GetBytes(value));
        }

        float normaliseValue(float value, float i0, float i1, float j0, float j1)
        {
            float L = (j0 - j1) / (i0 - i1);
            return (j0 - (L * i0) + (L * value));
        }

        public bool isBool(string value)
        {
            bool res = false;
            return bool.TryParse(value, out res);
        }

        public bool isInt(string value)
        {
            int res = 0;
            return int.TryParse(value, out res);
        }
        public bool isFloat(string value)
        {
            float res = 0f;
            return float.TryParse(value, out res);
        }

        public bool isDateTime(string value)
        {
            DateTime res = new DateTime();
            return DateTime.TryParse(value, out res);
        }

        // ------------------------- DATA ANALYTICS HELPER FUNCTIONS -----------------------

        public int nearestValue(List<float> list, float find)
        {            
            int index = list.BinarySearch(find);
            if (0 <= index)
               return index;// Console.Out.WriteLine("Found value {0} at list[{1}]", find, index);
            else
            {
                index = ~index;
                if (0 < index)
                return index-1;//Console.Out.WriteLine("list[{0}] = {1}", index - 1, list[index - 1]);
                else return index;//
//                Console.Out.WriteLine("list[{0}] = {1}", index, list[index]);
                //Console.Out.WriteLine("value {0} should be inserted at index {1}", find, index);
                // to insert
  //              list.Insert(index, find);
            }
        }
        /// <summary>
        /// Gets the number of categories that a *categorical* data dimension contains
        /// </summary>
        /// <param name="dimension"></param>
        /// <returns></returns>

        public List<float> getNumberOfCategories(int dimension)
        {
            return getNumberOfCategories(indexToDimension(dimension));
        }

        /// <summary>
        /// Gets the number of categories that a *categorical* data dimension contains
        /// </summary>
        /// <param name="dimension"></param>
        /// <returns></returns>
        /// 
        public List<float> getNumberOfCategories(string dimension)
        {
            float[] column = getDimension(dimension);
            List<float> values = new List<float>();
            for (int i = 0; i < column.Length; i++)
            {
                if (!values.Contains(column[i]))
                {
                    values.Add(column[i]);
                }
                //if (column[i] != column[i + 1])
                //{ Debug.Log(column[i] + "       " + column[i + 1]); categories++; }
            }
            return values;
        }



        #region oldcode creating class on the fly
        public object createClassOnTheFly(string data)
        {

            string[] lines = data.Split('\n');

            //1: read types
            string[] identifiers = lines[0].Split(split);
            string[] typesToRead = lines[1].Split(split);
            string[] theTypes = new string[typesToRead.Length];

            for (int i = 0; i < typesToRead.Length; i++)
            {
                if (isBool(typesToRead[i]))
                    theTypes[i] = "bool";
                else if (isFloat(typesToRead[i]))
                    theTypes[i] = "float";
                else theTypes[i] = "string";
            }

            string DataObject = createClass(identifiers, theTypes);

            Debug.Log(DataObject);

            //compile the class
            CodeDomProvider oCodeDomProvider = CodeDomProvider.CreateProvider("CSharp");
            // Add what referenced assemblies
            CompilerParameters oCompilerParameters = new CompilerParameters();
            oCompilerParameters.ReferencedAssemblies.Add("system.dll");
            // set the compiler to create a DLL
            oCompilerParameters.GenerateExecutable = false;
            // set the dll to be created in memory and not on the hard drive
            oCompilerParameters.GenerateInMemory = true;

            var oCompilerResults =
              oCodeDomProvider.CompileAssemblyFromSource(oCompilerParameters, DataObject);

            if (oCompilerResults.Errors.Count != 0)
                Debug.Log("There were errors while compiling the Data ");

            var oAssembly = oCompilerResults.CompiledAssembly;
            object oObject = oAssembly.CreateInstance("DataObject");
            Type _DataStructureType = oObject.GetType();
            //Debug.Log(GetPropValue(oObject,"CerealName"));
            FieldInfo[] fi = oObject.GetType().GetFields();

            foreach (FieldInfo info in fi)
            {
                Debug.Log(info.Name + " is type of " + info.FieldType.Name);
            }

            //create the datastructure


            for (int i = 1; i < lines.Length - 1; i++)
            {
                string[] values = lines[i].Split(',');
                object DataStructure = Activator.CreateInstance(_DataStructureType);

            }

            return oObject;
        }

        public object GetPropValue(object src, string propName)
        {
            return src.GetType().GetProperty(propName).GetValue(src, null);
        }

        public string createClass(string[] ids, string[] typeFields)
        {
            string classCompile =
                @"
              
              public class DataObject{
              " + "\n";

            string fields = "";
            for (int i = 0; i < typeFields.Length; i++)
            {
                fields += "public " + typeFields[i] + " " + ids[i] + ";\n";

            }
            classCompile += fields;
            classCompile += "}\n";

            return classCompile;
        }

        #endregion
    }
}