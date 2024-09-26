// <auto-generated>
//  automatically generated by the FlatBuffers compiler, do not modify
// </auto-generated>

namespace GameProgress.Codegen
{

using global::System;
using global::System.Collections.Generic;
using global::Google.FlatBuffers;

public struct NamedMap : IFlatbufferObject
{
  private Table __p;
  public ByteBuffer ByteBuffer { get { return __p.bb; } }
  public static void ValidateVersion() { FlatBufferConstants.FLATBUFFERS_24_3_25(); }
  public static NamedMap GetRootAsNamedMap(ByteBuffer _bb) { return GetRootAsNamedMap(_bb, new NamedMap()); }
  public static NamedMap GetRootAsNamedMap(ByteBuffer _bb, NamedMap obj) { return (obj.__assign(_bb.GetInt(_bb.Position) + _bb.Position, _bb)); }
  public void __init(int _i, ByteBuffer _bb) { __p = new Table(_i, _bb); }
  public NamedMap __assign(int _i, ByteBuffer _bb) { __init(_i, _bb); return this; }

  public string Name { get { int o = __p.__offset(4); return o != 0 ? __p.__string(o + __p.bb_pos) : null; } }
#if ENABLE_SPAN_T
  public Span<byte> GetNameBytes() { return __p.__vector_as_span<byte>(4, 1); }
#else
  public ArraySegment<byte>? GetNameBytes() { return __p.__vector_as_arraysegment(4); }
#endif
  public byte[] GetNameArray() { return __p.__vector_as_array<byte>(4); }
  public GameProgress.Codegen.Modes? Modes { get { int o = __p.__offset(6); return o != 0 ? (GameProgress.Codegen.Modes?)(new GameProgress.Codegen.Modes()).__assign(__p.__indirect(o + __p.bb_pos), __p.bb) : null; } }

  public static Offset<GameProgress.Codegen.NamedMap> CreateNamedMap(FlatBufferBuilder builder,
      StringOffset nameOffset = default(StringOffset),
      Offset<GameProgress.Codegen.Modes> modesOffset = default(Offset<GameProgress.Codegen.Modes>)) {
    builder.StartTable(2);
    NamedMap.AddModes(builder, modesOffset);
    NamedMap.AddName(builder, nameOffset);
    return NamedMap.EndNamedMap(builder);
  }

  public static void StartNamedMap(FlatBufferBuilder builder) { builder.StartTable(2); }
  public static void AddName(FlatBufferBuilder builder, StringOffset nameOffset) { builder.AddOffset(0, nameOffset.Value, 0); }
  public static void AddModes(FlatBufferBuilder builder, Offset<GameProgress.Codegen.Modes> modesOffset) { builder.AddOffset(1, modesOffset.Value, 0); }
  public static Offset<GameProgress.Codegen.NamedMap> EndNamedMap(FlatBufferBuilder builder) {
    int o = builder.EndTable();
    return new Offset<GameProgress.Codegen.NamedMap>(o);
  }
  public NamedMapT UnPack() {
    var _o = new NamedMapT();
    this.UnPackTo(_o);
    return _o;
  }
  public void UnPackTo(NamedMapT _o) {
    _o.Name = this.Name;
    _o.Modes = this.Modes.HasValue ? this.Modes.Value.UnPack() : null;
  }
  public static Offset<GameProgress.Codegen.NamedMap> Pack(FlatBufferBuilder builder, NamedMapT _o) {
    if (_o == null) return default(Offset<GameProgress.Codegen.NamedMap>);
    var _name = _o.Name == null ? default(StringOffset) : builder.CreateString(_o.Name);
    var _modes = _o.Modes == null ? default(Offset<GameProgress.Codegen.Modes>) : GameProgress.Codegen.Modes.Pack(builder, _o.Modes);
    return CreateNamedMap(
      builder,
      _name,
      _modes);
  }
}

public class NamedMapT
{
  public string Name { get; set; }
  public GameProgress.Codegen.ModesT Modes { get; set; }

  public NamedMapT() {
    this.Name = null;
    this.Modes = null;
  }
}


static public class NamedMapVerify
{
  static public bool Verify(Google.FlatBuffers.Verifier verifier, uint tablePos)
  {
    return verifier.VerifyTableStart(tablePos)
      && verifier.VerifyString(tablePos, 4 /*Name*/, false)
      && verifier.VerifyTable(tablePos, 6 /*Modes*/, GameProgress.Codegen.ModesVerify.Verify, false)
      && verifier.VerifyTableEnd(tablePos);
  }
}

}