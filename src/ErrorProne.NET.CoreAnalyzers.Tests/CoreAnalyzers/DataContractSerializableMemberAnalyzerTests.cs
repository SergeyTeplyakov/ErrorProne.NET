using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = ErrorProne.NET.TestHelpers.CSharpCodeFixVerifier<
    ErrorProne.NET.CoreAnalyzers.DataContractSerializableMemberAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace ErrorProne.NET.CoreAnalyzers.Tests.CoreAnalyzers
{
    [TestFixture]
    public class DataContractSerializableMemberAnalyzerTests
    {
        [Test]
        public async Task Warn_On_IPAddress_Property()
        {
            string code = @"
using System.Net;
using System.Runtime.Serialization;

[DataContract]
public class MyContract
{
    [DataMember]
    public IPAddress [|Address|] { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_IPAddress_Field()
        {
            string code = @"
using System.Net;
using System.Runtime.Serialization;

[DataContract]
public class MyContract
{
    [DataMember]
    public IPAddress [|Address|];
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_IPEndPoint()
        {
            string code = @"
using System.Net;
using System.Runtime.Serialization;

[DataContract]
public class MyContract
{
    [DataMember]
    public IPEndPoint [|EndPoint|] { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Collection_Of_IPAddress()
        {
            string code = @"
using System.Collections.Generic;
using System.Net;
using System.Runtime.Serialization;

[DataContract]
public class MyContract
{
    [DataMember]
    public List<IPAddress> [|Addresses|] { get; set; }

    [DataMember]
    public IPAddress[] [|MoreAddresses|] { get; set; }

    [DataMember]
    public Dictionary<string, IPAddress> [|Map|] { get; set; }

    [DataMember]
    public IEnumerable<IPAddress> [|Sequence|] { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_User_Type_Without_Parameterless_Constructor()
        {
            string code = @"
using System.Runtime.Serialization;

public class NoParameterlessCtor
{
    public NoParameterlessCtor(int x) { X = x; }
    public int X { get; set; }
}

[DataContract]
public class MyContract
{
    [DataMember]
    public NoParameterlessCtor [|Value|] { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Non_Public_Type()
        {
            string code = @"
using System.Runtime.Serialization;

internal class InternalPoco
{
    public int X { get; set; }
}

[DataContract]
internal class MyContract
{
    [DataMember]
    public InternalPoco [|Value|] { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Struct_In_A_Data_Contract_Struct()
        {
            string code = @"
using System.Net;
using System.Runtime.Serialization;

[DataContract]
public struct MyContract
{
    [DataMember]
    public IPAddress [|Address|] { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Nullable_And_Nested_Generics()
        {
            string code = @"
using System.Collections.Generic;
using System.Runtime.Serialization;

public struct BadStruct
{
    // The struct itself is fine, but it contains a bad member.
    public int X { get; set; }
}

public class NoCtor { public NoCtor(int x) { } }

[DataContract]
public class MyContract
{
    [DataMember]
    public List<Dictionary<string, NoCtor>> [|Nested|] { get; set; }

    [DataMember]
    public BadStruct? Fine { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_On_Non_DataMember_Members()
        {
            string code = @"
using System.Net;
using System.Runtime.Serialization;

[DataContract]
public class MyContract
{
    [DataMember]
    public string Name { get; set; }

    // Not marked with [DataMember], so it is not serialized.
    public IPAddress Address { get; set; }

    [IgnoreDataMember]
    public IPAddress AnotherAddress { get; set; }

    public static IPAddress StaticAddress { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_When_Type_Is_Not_A_DataContract()
        {
            string code = @"
using System.Net;

public class NotAContract
{
    public IPAddress Address { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_On_Serializable_And_DataContract_Types()
        {
            string code = @"
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

[Serializable]
public class SerializableNoCtor
{
    public SerializableNoCtor(int x) { }
}

[DataContract]
public class NestedContract
{
    [DataMember]
    public int X { get; set; }
}

[DataContract]
public class MyContract
{
    [DataMember]
    public SerializableNoCtor Serializable { get; set; }

    [DataMember]
    public NestedContract Nested { get; set; }

    [DataMember]
    public Uri Url { get; set; }

    [DataMember]
    public Version Version { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_On_Primitives_Enums_And_Collections()
        {
            string code = @"
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

public enum MyEnum { One }

[DataContract]
public class MyContract
{
    [DataMember]
    public int Number { get; set; }

    [DataMember]
    public string Name { get; set; }

    [DataMember]
    public int? NullableNumber { get; set; }

    [DataMember]
    public MyEnum Enum { get; set; }

    [DataMember]
    public Guid Id { get; set; }

    [DataMember]
    public DateTime Date { get; set; }

    [DataMember]
    public TimeSpan Duration { get; set; }

    [DataMember]
    public byte[] Blob { get; set; }

    [DataMember]
    public List<string> Names { get; set; }

    [DataMember]
    public Dictionary<string, int> Map { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_On_Private_Parameterless_Constructor()
        {
            string code = @"
using System.Runtime.Serialization;

public class PrivateCtor
{
    private PrivateCtor() { }
    public static PrivateCtor Create() => new PrivateCtor();
    public int X { get; set; }
}

[DataContract]
public class MyContract
{
    [DataMember]
    public PrivateCtor Value { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_On_Abstract_Interface_And_Object_Members()
        {
            string code = @"
using System.Runtime.Serialization;

public abstract class AbstractBase
{
    public AbstractBase(int x) { }
}

public interface IThing { }

[DataContract]
public class MyContract
{
    [DataMember]
    public AbstractBase Base { get; set; }

    [DataMember]
    public IThing Thing { get; set; }

    [DataMember]
    public object Any { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task NoWarn_On_Generic_Type_Parameters()
        {
            string code = @"
using System.Runtime.Serialization;

[DataContract]
public class MyContract<T>
{
    [DataMember]
    public T Value { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }

        [Test]
        public async Task Warn_On_Positional_Record_Member_Type()
        {
            // A positional record has no parameterless constructor and is not serializable.
            string code = @"
using System.Runtime.Serialization;

public record PositionalRecord(int X, string Name);

[DataContract]
public class MyContract
{
    [DataMember]
    public PositionalRecord [|Value|] { get; set; }
}";

            await VerifyCS.VerifyAsync(code, LanguageVersion.CSharp10);
        }

        [Test]
        public async Task Warn_On_Bad_Member_Of_A_Record_Data_Contract()
        {
            string code = @"
using System.Net;
using System.Runtime.Serialization;

[DataContract]
public record MyContract
{
    [DataMember]
    public IPAddress [|Address|] { get; set; }
}";

            await VerifyCS.VerifyAsync(code, LanguageVersion.CSharp10);
        }

        [Test]
        public async Task Warn_On_Bad_Positional_Member_Of_A_Record_Data_Contract()
        {
            string code = @"
using System.Net;
using System.Runtime.Serialization;

[DataContract]
public record MyContract([property: DataMember] int X, [property: DataMember] IPAddress [|Address|]);";

            await VerifyCS.VerifyAsync(code, LanguageVersion.CSharp10);
        }

        [Test]
        public async Task Warn_On_Bad_Member_Of_A_Record_Struct_Data_Contract()
        {
            string code = @"
using System.Net;
using System.Runtime.Serialization;

[DataContract]
public record struct MyContract
{
    [DataMember]
    public IPAddress [|Address|] { get; set; }
}";

            await VerifyCS.VerifyAsync(code, LanguageVersion.CSharp10);
        }

        [Test]
        public async Task NoWarn_On_Record_Struct_And_Record_With_Parameterless_Constructor()
        {
            string code = @"
using System.Runtime.Serialization;

public record struct PositionalRecordStruct(int X);

public record RecordWithBody
{
    public int X { get; set; }
}

[DataContract]
public class MyContract
{
    [DataMember]
    public PositionalRecordStruct Value { get; set; }

    [DataMember]
    public RecordWithBody Another { get; set; }
}";

            await VerifyCS.VerifyAsync(code, LanguageVersion.CSharp10);
        }

        [Test]
        public async Task NoWarn_On_Synthesized_Record_Members()
        {
            // 'EqualityContract' and the other compiler-generated members must not be analyzed.
            string code = @"
using System.Runtime.Serialization;

[DataContract]
public record MyContract(int X)
{
    [DataMember]
    public string Name { get; set; }
}";

            await VerifyCS.VerifyAsync(code, LanguageVersion.CSharp10);
        }

        [Test]
        public async Task NoWarn_On_Positional_Record_Member_Without_DataMember()
        {
            // Without '[property: DataMember]' the positional member is not serialized at all.
            string code = @"
using System.Net;
using System.Runtime.Serialization;

[DataContract]
public record MyContract(IPAddress Address);";

            await VerifyCS.VerifyAsync(code, LanguageVersion.CSharp10);
        }

        [Test]
        public async Task NoWarn_On_ISerializable_Implementation()
        {
            string code = @"
using System.Runtime.Serialization;

public class CustomSerializable : ISerializable
{
    public CustomSerializable(int x) { }
    public void GetObjectData(SerializationInfo info, StreamingContext context) { }
}

[DataContract]
public class MyContract
{
    [DataMember]
    public CustomSerializable Value { get; set; }
}";

            await VerifyCS.VerifyAsync(code);
        }
    }
}
