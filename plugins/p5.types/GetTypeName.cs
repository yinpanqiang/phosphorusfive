/*
 * Phosphorus Five, copyright 2014 - 2015, Thomas Hansen, phosphorusfive@gmail.com
 * Phosphorus Five is licensed under the terms of the MIT license, see the enclosed LICENSE file for details
 */

using p5.core;

namespace p5.types
{
    /// <summary>
    ///     Helper class for retrieving Hyperlisp type-names of types.
    /// 
    ///     Class containing Active Events necessary to determine the Hyperlisp type-name of types.
    /// </summary>
    public static class GetTypeName
    {
        /*
         * hyperlisp type name Active Events for converting fully qualified typenames into hyperlisp typenames. if you
         * create your own types that you wish to store in hyperlisp using some sort of string representation, representing
         * your objects, then you need to implement your own hyperlisp type name conversion Active Events. these Active Events
         * must be named "p5.hyperlisp.get-type-name.*", where "*" is the fully qualified name of your type, meaning
         * typeof(MyType).FullName
         * 
         * if you wish for hyperlisp to support your own custom types, you will have to implement three Active Events;
         * 
         *   1. "p5.hyperlisp.get-type-name.*" - (*) being the fully qualified name of your type, or typeof (YourType).FullName,
         *      that returns your hyperlisp typename
         * 
         *   2. "p5.hyperlisp.get-string-value.*" - (*) being the fully qualified name of your type, or typeof (YourType).FullName,
         *      that returns a string representing your object
         * 
         *   3. "p5.hyperlisp.get-object-value.*" - (*) being the hyperlisp typename of your type, 
         *      returned from your "p5.hyperlisp.get-type-name.*",
         *      that returns an object created from the string representing your object
         */

        /*
         * retrieving type name active events
         */

        /// <summary>
        ///     Returns the Hyperlisp type-name for the Node type.
        /// 
        ///     Returns the Hyperlisp type-name for <see cref="phosphorus.core.Node">Node</see>s, which is <em>"node"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.p5.core.Node", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_p5_core_Node (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "node";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the DNA type.
        /// 
        ///     Returns the Hyperlisp type-name for <see cref="phosphorus.core.Node.Dna">DNA</see>s, which is <em>"path"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.p5.core.Node+Dna", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_p5_core_Node_DNA (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "path";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the Expression type.
        /// 
        ///     Returns the Hyperlisp type-name for <see cref="p5.exp.Expression">Expressions</see>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.p5.exp.Expression", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_p5_exp_Expression (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "x";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the Guid type.
        /// 
        ///     Returns the Hyperlisp type-name for System.Guid, which is <em>"guid"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.Guid", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_Guid (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "guid";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the long type.
        /// 
        ///     Returns the Hyperlisp type-name for System.Int64, which is <em>"long"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.Int64", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_Int64 (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "long";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the ulong type.
        /// 
        ///     Returns the Hyperlisp type-name for System.UInt64, which is <em>"ulong"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.UInt64", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_UInt64 (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "ulong";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the int type.
        /// 
        ///     Returns the Hyperlisp type-name for System.Int32, which is <em>"int"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.Int32", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_Int32 (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "int";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the uint type.
        /// 
        ///     Returns the Hyperlisp type-name for System.UInt32, which is <em>"uint"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.UInt32", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_UInt32 (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "uint";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the short type.
        /// 
        ///     Returns the Hyperlisp type-name for System.Int16, which is <em>"short"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.Int16", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_Int16 (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "short";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the ushort type.
        /// 
        ///     Returns the Hyperlisp type-name for System.UInt16, which is <em>"ushort"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.UInt16", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_UInt16 (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "ushort";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the single type.
        /// 
        ///     Returns the Hyperlisp type-name for System.Single, which is <em>"float"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.Single", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_Single (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "float";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the double type.
        /// 
        ///     Returns the Hyperlisp type-name for System.Double, which is <em>"double"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.Double", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_Double (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "double";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the decimal type.
        /// 
        ///     Returns the Hyperlisp type-name for System.Decimal, which is <em>"decimal"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.Decimal", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_Decimal (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "decimal";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the bool type.
        /// 
        ///     Returns the Hyperlisp type-name for System.Boolean, which is <em>"bool"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.Boolean", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_Boolean (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "bool";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the byte type.
        /// 
        ///     Returns the Hyperlisp type-name for System.Byte, which is <em>"byte"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.Byte", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_Byte (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "byte";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the byte array type.
        /// 
        ///     Returns the Hyperlisp type-name for System.Byte[], which is <em>"blob"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.Byte[]", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_ByteBlob (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "blob";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the sbyte type.
        /// 
        ///     Returns the Hyperlisp type-name for System.SByte, which is <em>"sbyte"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.SByte", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_SByte (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "sbyte";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the char type.
        /// 
        ///     Returns the Hyperlisp type-name for System.Char, which is <em>"char"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.Char", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_Char (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "char";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the date type.
        /// 
        ///     Returns the Hyperlisp type-name for System.DateTime, which is <em>"date"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.DateTime", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_DateTime (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "date";
        }

        /// <summary>
        ///     Returns the Hyperlisp type-name for the timespan type.
        /// 
        ///     Returns the Hyperlisp type-name for System.TimeSpan, which is <em>"time"</em>.
        /// </summary>
        /// <param name="context">Application context</param>
        /// <param name="e">Parameters passed into Active Event</param>
        [ActiveEvent (Name = "p5.hyperlisp.get-type-name.System.TimeSpan", Protection = EntranceProtection.NativeOnly)]
        private static void p5_hyperlisp_get_type_name_System_TimeSpan (ApplicationContext context, ActiveEventArgs e)
        {
            e.Args.Value = "time";
        }
    }
}
