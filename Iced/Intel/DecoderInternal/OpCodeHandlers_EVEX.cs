/*
Copyright (C) 2018-2019 de4dot@gmail.com

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

#if !NO_DECODER
using System;
using System.Diagnostics;

namespace Iced.Intel.DecoderInternal {
	sealed class OpCodeHandler_VectorLength_EVEX : OpCodeHandlerModRM {
		readonly OpCodeHandler[] handlers;

		public OpCodeHandler_VectorLength_EVEX(OpCodeHandler handler128, OpCodeHandler handler256, OpCodeHandler handler512) {
			Debug.Assert((int)VectorLength.L128 == 0);
			Debug.Assert((int)VectorLength.L256 == 1);
			Debug.Assert((int)VectorLength.L512 == 2);
			Debug.Assert((int)VectorLength.Unknown == 3);
			handlers = new OpCodeHandler[4] {
				handler128 ?? throw new ArgumentNullException(nameof(handler128)),
				handler256 ?? throw new ArgumentNullException(nameof(handler256)),
				handler512 ?? throw new ArgumentNullException(nameof(handler512)),
				OpCodeHandler_Invalid.Instance,
			};
			Debug.Assert(handler128.HasModRM == HasModRM);
			Debug.Assert(handler256.HasModRM == HasModRM);
			Debug.Assert(handler512.HasModRM == HasModRM);
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			Debug.Assert(decoder.state.Encoding == EncodingKind.EVEX);
			handlers[(int)decoder.state.vectorLength].Decode(decoder, ref instruction);
		}
	}

	sealed class OpCodeHandler_VectorLength_EVEX_er : OpCodeHandlerModRM {
		readonly OpCodeHandler[] handlers;

		public OpCodeHandler_VectorLength_EVEX_er(OpCodeHandler handler128, OpCodeHandler handler256, OpCodeHandler handler512) {
			Debug.Assert((int)VectorLength.L128 == 0);
			Debug.Assert((int)VectorLength.L256 == 1);
			Debug.Assert((int)VectorLength.L512 == 2);
			Debug.Assert((int)VectorLength.Unknown == 3);
			handlers = new OpCodeHandler[4] {
				handler128 ?? throw new ArgumentNullException(nameof(handler128)),
				handler256 ?? throw new ArgumentNullException(nameof(handler256)),
				handler512 ?? throw new ArgumentNullException(nameof(handler512)),
				OpCodeHandler_Invalid.Instance,
			};
			Debug.Assert(handler128.HasModRM == HasModRM);
			Debug.Assert(handler256.HasModRM == HasModRM);
			Debug.Assert(handler512.HasModRM == HasModRM);
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			int index = (int)state.vectorLength;
			if (state.mod == 3 && (state.flags & StateFlags.b) != 0)
				index = (int)VectorLength.L512;
			handlers[index].Decode(decoder, ref instruction);
		}
	}

	sealed class OpCodeHandler_EVEX : OpCodeHandlerModRM {
		readonly OpCodeHandler handlerMem;

		public OpCodeHandler_EVEX(OpCodeHandler handlerMem) => this.handlerMem = handlerMem ?? throw new ArgumentNullException(nameof(handlerMem));

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			if (decoder.is64Mode)
				decoder.EVEX_MVEX(ref instruction);
			else if (decoder.state.mod == 3)
				decoder.EVEX_MVEX(ref instruction);
			else
				handlerMem.Decode(decoder, ref instruction);
		}
	}

	sealed class OpCodeHandler_EVEX_V_H_Ev_er : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code codeW0;
		readonly Code codeW1;
		readonly TupleType tupleType;
		readonly bool onlySAE;
		readonly bool noERd;

		public OpCodeHandler_EVEX_V_H_Ev_er(Register baseReg, Code codeW0, Code codeW1, TupleType tupleType, bool onlySAE, bool noERd) {
			this.baseReg = baseReg;
			this.codeW0 = codeW0;
			this.codeW1 = codeW1;
			this.tupleType = tupleType;
			this.onlySAE = onlySAE;
			this.noERd = noERd;
		}

		public OpCodeHandler_EVEX_V_H_Ev_er(Register baseReg, Code codeW0, Code codeW1, TupleType tupleType, bool onlySAE) {
			this.baseReg = baseReg;
			this.codeW0 = codeW0;
			this.codeW1 = codeW1;
			this.tupleType = tupleType;
			this.onlySAE = onlySAE;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (state.aaa | state.extraBaseRegisterBaseEVEX) != 0)
				decoder.SetInvalidInstruction();
			Register gpr;
			if ((state.flags & StateFlags.W) != 0 && decoder.is64Mode) {
				instruction.InternalCode = codeW1;
				gpr = Register.RAX;
			}
			else {
				instruction.InternalCode = codeW0;
				gpr = Register.EAX;
			}
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp2Kind = OpKind.Register;
				instruction.InternalOp2Register = (int)(state.rm + state.extraBaseRegisterBase) + gpr;
				if ((state.flags & StateFlags.b) != 0 && (!noERd || ((state.flags & StateFlags.W) != 0 && decoder.is64Mode))) {
					if (onlySAE)
						instruction.InternalSetSuppressAllExceptions();
					else {
						Debug.Assert((int)RoundingControl.None == 0);
						Debug.Assert((int)RoundingControl.RoundToNearest == 1);
						Debug.Assert((int)RoundingControl.RoundDown == 2);
						Debug.Assert((int)RoundingControl.RoundUp == 3);
						Debug.Assert((int)RoundingControl.RoundTowardZero == 4);
						instruction.InternalRoundingControl = (uint)state.vectorLength + 1;
					}
				}
			}
			else {
				instruction.InternalOp2Kind = OpKind.Memory;
				decoder.ReadOpMem(ref instruction, tupleType);
			}
		}
	}

	sealed class OpCodeHandler_EVEX_V_H_Ev_Ib : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code codeW0;
		readonly Code codeW1;
		readonly TupleType tupleTypeW0;
		readonly TupleType tupleTypeW1;

		public OpCodeHandler_EVEX_V_H_Ev_Ib(Register baseReg, Code codeW0, Code codeW1, TupleType tupleTypeW0, TupleType tupleTypeW1) {
			this.baseReg = baseReg;
			this.codeW0 = codeW0;
			this.codeW1 = codeW1;
			this.tupleTypeW0 = tupleTypeW0;
			this.tupleTypeW1 = tupleTypeW1;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.b) | state.aaa | state.extraBaseRegisterBaseEVEX) != 0)
				decoder.SetInvalidInstruction();
			Register gpr;
			if ((state.flags & StateFlags.W) != 0 && decoder.is64Mode) {
				instruction.InternalCode = codeW1;
				gpr = Register.RAX;
			}
			else {
				instruction.InternalCode = codeW0;
				gpr = Register.EAX;
			}
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp2Kind = OpKind.Register;
				instruction.InternalOp2Register = (int)(state.rm + state.extraBaseRegisterBase) + gpr;
			}
			else {
				instruction.InternalOp2Kind = OpKind.Memory;
				if ((state.flags & StateFlags.W) != 0 && decoder.is64Mode)
					decoder.ReadOpMem(ref instruction, tupleTypeW1);
				else
					decoder.ReadOpMem(ref instruction, tupleTypeW0);
			}
			instruction.InternalOp3Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadByte();
		}
	}

	sealed class OpCodeHandler_EVEX_Ed_V_Ib : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code32;
		readonly Code code64;
		readonly TupleType tupleType32;
		readonly TupleType tupleType64;

		public OpCodeHandler_EVEX_Ed_V_Ib(Register baseReg, Code code32, Code code64, TupleType tupleType32, TupleType tupleType64) {
			this.baseReg = baseReg;
			this.code32 = code32;
			this.code64 = code64;
			this.tupleType32 = tupleType32;
			this.tupleType64 = tupleType64;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.b) | state.vvvv | state.aaa | state.extraBaseRegisterBaseEVEX) != 0)
				decoder.SetInvalidInstruction();
			Register gpr;
			if ((state.flags & StateFlags.W) != 0 && decoder.is64Mode) {
				instruction.InternalCode = code64;
				gpr = Register.RAX;
			}
			else {
				instruction.InternalCode = code32;
				gpr = Register.EAX;
			}
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp0Kind = OpKind.Register;
				instruction.InternalOp0Register = (int)(state.rm + state.extraBaseRegisterBase) + gpr;
			}
			else {
				instruction.InternalOp0Kind = OpKind.Memory;
				if ((state.flags & StateFlags.W) != 0 && decoder.is64Mode)
					decoder.ReadOpMem(ref instruction, tupleType64);
				else
					decoder.ReadOpMem(ref instruction, tupleType32);
			}
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg;
			instruction.InternalOp2Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadByte();
		}
	}

	sealed class OpCodeHandler_EVEX_VkHW_er : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code;
		readonly TupleType tupleType;
		readonly bool onlySAE;

		public OpCodeHandler_EVEX_VkHW_er(Register baseReg, Code code, TupleType tupleType, bool onlySAE) {
			this.baseReg = baseReg;
			this.code = code;
			this.tupleType = tupleType;
			this.onlySAE = onlySAE;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp2Kind = OpKind.Register;
				instruction.InternalOp2Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg;
				if ((state.flags & StateFlags.b) != 0) {
					if (onlySAE)
						instruction.InternalSetSuppressAllExceptions();
					else {
						Debug.Assert((int)RoundingControl.None == 0);
						Debug.Assert((int)RoundingControl.RoundToNearest == 1);
						Debug.Assert((int)RoundingControl.RoundDown == 2);
						Debug.Assert((int)RoundingControl.RoundUp == 3);
						Debug.Assert((int)RoundingControl.RoundTowardZero == 4);
						instruction.InternalRoundingControl = (uint)state.vectorLength + 1;
					}
				}
			}
			else {
				instruction.InternalOp2Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_VkW_er : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Code code;
		readonly TupleType tupleType;
		readonly bool onlySAE;

		public OpCodeHandler_EVEX_VkW_er(Register baseReg, Code code, TupleType tupleType, bool onlySAE) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			this.code = code;
			this.tupleType = tupleType;
			this.onlySAE = onlySAE;
		}

		public OpCodeHandler_EVEX_VkW_er(Register baseReg1, Register baseReg2, Code code, TupleType tupleType, bool onlySAE) {
			this.baseReg1 = baseReg1;
			this.baseReg2 = baseReg2;
			this.code = code;
			this.tupleType = tupleType;
			this.onlySAE = onlySAE;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && state.vvvv != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg1;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp1Kind = OpKind.Register;
				instruction.InternalOp1Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg2;
				if ((state.flags & StateFlags.b) != 0) {
					if (onlySAE)
						instruction.InternalSetSuppressAllExceptions();
					else {
						Debug.Assert((int)RoundingControl.None == 0);
						Debug.Assert((int)RoundingControl.RoundToNearest == 1);
						Debug.Assert((int)RoundingControl.RoundDown == 2);
						Debug.Assert((int)RoundingControl.RoundUp == 3);
						Debug.Assert((int)RoundingControl.RoundTowardZero == 4);
						instruction.InternalRoundingControl = (uint)state.vectorLength + 1;
					}
				}
			}
			else {
				instruction.InternalOp1Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_VkWIb_er : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Code code;
		readonly TupleType tupleType;
		readonly bool onlySAE;

		public OpCodeHandler_EVEX_VkWIb_er(Register baseReg, Code code, TupleType tupleType, bool onlySAE) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			this.code = code;
			this.tupleType = tupleType;
			this.onlySAE = onlySAE;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && state.vvvv != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg1;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp1Kind = OpKind.Register;
				instruction.InternalOp1Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg2;
				if ((state.flags & StateFlags.b) != 0) {
					if (onlySAE)
						instruction.InternalSetSuppressAllExceptions();
					else {
						Debug.Assert((int)RoundingControl.None == 0);
						Debug.Assert((int)RoundingControl.RoundToNearest == 1);
						Debug.Assert((int)RoundingControl.RoundDown == 2);
						Debug.Assert((int)RoundingControl.RoundUp == 3);
						Debug.Assert((int)RoundingControl.RoundTowardZero == 4);
						instruction.InternalRoundingControl = (uint)state.vectorLength + 1;
					}
				}
			}
			else {
				instruction.InternalOp1Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOp2Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadIb();
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_VkW : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_VkW(Register baseReg, Code code, TupleType tupleType) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public OpCodeHandler_EVEX_VkW(Register baseReg1, Register baseReg2, Code code, TupleType tupleType) {
			this.baseReg1 = baseReg1;
			this.baseReg2 = baseReg2;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && state.vvvv != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg1;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp1Kind = OpKind.Register;
				instruction.InternalOp1Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg2;
			}
			else {
				instruction.InternalOp1Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_WkV : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_WkV(Register baseReg, Code code, TupleType tupleType) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public OpCodeHandler_EVEX_WkV(Register baseReg1, Register baseReg2, Code code, TupleType tupleType) {
			this.baseReg1 = baseReg1;
			this.baseReg2 = baseReg2;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.b) | state.vvvv) != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp0Kind = OpKind.Register;
				instruction.InternalOp0Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg1;
			}
			else {
				instruction.InternalOp0Kind = OpKind.Memory;
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg2;
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_VkM : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_VkM(Register baseReg, Code code, TupleType tupleType) {
			this.baseReg = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && state.vvvv != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg;
			if (state.mod == 3)
				decoder.SetInvalidInstruction();
			else {
				instruction.InternalOp1Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_VkWIb : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_VkWIb(Register baseReg, Code code, TupleType tupleType) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && state.vvvv != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg1;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp1Kind = OpKind.Register;
				instruction.InternalOp1Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg2;
			}
			else {
				instruction.InternalOp1Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOp2Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadIb();
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_WkVIb : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_WkVIb(Register baseReg1, Register baseReg2, Code code, TupleType tupleType) {
			this.baseReg1 = baseReg1;
			this.baseReg2 = baseReg2;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && state.vvvv != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp0Kind = OpKind.Register;
				instruction.InternalOp0Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg1;
			}
			else {
				instruction.InternalOp0Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg2;
			instruction.InternalOp2Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadIb();
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_HkWIb : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_HkWIb(Register baseReg, Code code, TupleType tupleType) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)state.vvvv + baseReg1;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp1Kind = OpKind.Register;
				instruction.InternalOp1Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg2;
			}
			else {
				instruction.InternalOp1Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOp2Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadIb();
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_WkVIb_er : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Code code;
		readonly TupleType tupleType;
		readonly bool onlySAE;

		public OpCodeHandler_EVEX_WkVIb_er(Register baseReg1, Register baseReg2, Code code, TupleType tupleType, bool onlySAE) {
			this.baseReg1 = baseReg1;
			this.baseReg2 = baseReg2;
			this.code = code;
			this.tupleType = tupleType;
			this.onlySAE = onlySAE;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && state.vvvv != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp0Kind = OpKind.Register;
				instruction.InternalOp0Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg1;
				if ((state.flags & StateFlags.b) != 0) {
					if (onlySAE)
						instruction.InternalSetSuppressAllExceptions();
					else {
						Debug.Assert((int)RoundingControl.None == 0);
						Debug.Assert((int)RoundingControl.RoundToNearest == 1);
						Debug.Assert((int)RoundingControl.RoundDown == 2);
						Debug.Assert((int)RoundingControl.RoundUp == 3);
						Debug.Assert((int)RoundingControl.RoundTowardZero == 4);
						instruction.InternalRoundingControl = (uint)state.vectorLength + 1;
					}
				}
			}
			else {
				instruction.InternalOp0Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg2;
			instruction.InternalOp2Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadIb();
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_VW_er : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Code code;
		readonly TupleType tupleType;
		readonly bool onlySAE;

		public OpCodeHandler_EVEX_VW_er(Register baseReg, Code code, TupleType tupleType, bool onlySAE) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			this.code = code;
			this.tupleType = tupleType;
			this.onlySAE = onlySAE;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg1;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp1Kind = OpKind.Register;
				instruction.InternalOp1Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg2;
				if ((state.flags & StateFlags.b) != 0) {
					if (onlySAE)
						instruction.InternalSetSuppressAllExceptions();
					else {
						Debug.Assert((int)RoundingControl.None == 0);
						Debug.Assert((int)RoundingControl.RoundToNearest == 1);
						Debug.Assert((int)RoundingControl.RoundDown == 2);
						Debug.Assert((int)RoundingControl.RoundUp == 3);
						Debug.Assert((int)RoundingControl.RoundTowardZero == 4);
						instruction.InternalRoundingControl = (uint)state.vectorLength + 1;
					}
				}
			}
			else {
				instruction.InternalOp1Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (state.vvvv | state.aaa | (uint)(state.flags & StateFlags.z)) != 0)
				decoder.SetInvalidInstruction();
		}
	}

	sealed class OpCodeHandler_EVEX_VW : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_VW(Register baseReg, Code code, TupleType tupleType) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (state.vvvv | state.aaa) != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg1;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp1Kind = OpKind.Register;
				instruction.InternalOp1Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg2;
			}
			else {
				instruction.InternalOp1Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
		}
	}

	sealed class OpCodeHandler_EVEX_WV : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_WV(Register baseReg, Code code, TupleType tupleType) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (state.vvvv | state.aaa) != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp0Kind = OpKind.Register;
				instruction.InternalOp0Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg2;
			}
			else {
				instruction.InternalOp0Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg1;
		}
	}

	sealed class OpCodeHandler_EVEX_VM : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_VM(Register baseReg, Code code, TupleType tupleType) {
			this.baseReg = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (state.vvvv | state.aaa) != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg;
			if (state.mod == 3)
				decoder.SetInvalidInstruction();
			else {
				instruction.InternalOp1Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
		}
	}

	sealed class OpCodeHandler_EVEX_VK : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code;

		public OpCodeHandler_EVEX_VK(Register baseReg, Code code) {
			this.baseReg = baseReg;
			this.code = code;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.b) | state.vvvv | state.aaa | state.extraBaseRegisterBase | state.extraBaseRegisterBaseEVEX) != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp1Kind = OpKind.Register;
				instruction.InternalOp1Register = (int)state.rm + Register.K0;
			}
			else
				decoder.SetInvalidInstruction();
		}
	}

	sealed class OpCodeHandler_EVEX_KR : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code;

		public OpCodeHandler_EVEX_KR(Register baseReg, Code code) {
			this.baseReg = baseReg;
			this.code = code;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.b) | state.vvvv | state.aaa | state.extraRegisterBase | state.extraRegisterBaseEVEX) != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)state.reg + Register.K0;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp1Kind = OpKind.Register;
				instruction.InternalOp1Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg;
			}
			else
				decoder.SetInvalidInstruction();
		}
	}

	sealed class OpCodeHandler_EVEX_kkHWIb : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_kkHWIb(Register baseReg, Code code, TupleType tupleType) {
			this.baseReg = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((state.extraRegisterBase + state.extraRegisterBaseEVEX) != 0 || (state.reg == state.aaa && state.aaa != 0)))
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)state.reg + Register.K0;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp2Kind = OpKind.Register;
				instruction.InternalOp2Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg;
				if ((state.flags & StateFlags.b) != 0)
					instruction.InternalSetSuppressAllExceptions();
			}
			else {
				instruction.InternalOp2Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOp3Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadByte();
			instruction.InternalOpMask = state.aaa;
		}
	}

	sealed class OpCodeHandler_EVEX_VkHW : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Register baseReg3;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_VkHW(Register baseReg, Code code, TupleType tupleType) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			baseReg3 = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public OpCodeHandler_EVEX_VkHW(Register baseReg1, Register baseReg2, Register baseReg3, Code code, TupleType tupleType) {
			this.baseReg1 = baseReg1;
			this.baseReg2 = baseReg2;
			this.baseReg3 = baseReg3;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg1;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg2;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp2Kind = OpKind.Register;
				instruction.InternalOp2Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg3;
				if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (state.flags & StateFlags.b) != 0)
					decoder.SetInvalidInstruction();
			}
			else {
				instruction.InternalOp2Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_VkHM : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_VkHM(Register baseReg, Code code, TupleType tupleType) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg1;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg2;
			if (state.mod == 3)
				decoder.SetInvalidInstruction();
			else {
				instruction.InternalOp2Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_VkHWIb : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Register baseReg3;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_VkHWIb(Register baseReg, Code code, TupleType tupleType) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			baseReg3 = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public OpCodeHandler_EVEX_VkHWIb(Register baseReg1, Register baseReg2, Register baseReg3, Code code, TupleType tupleType) {
			this.baseReg1 = baseReg1;
			this.baseReg2 = baseReg2;
			this.baseReg3 = baseReg3;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg1;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg2;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp2Kind = OpKind.Register;
				instruction.InternalOp2Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg3;
				if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (state.flags & StateFlags.b) != 0)
					decoder.SetInvalidInstruction();
			}
			else {
				instruction.InternalOp2Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOp3Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadIb();
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_VkHWIb_er : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Register baseReg3;
		readonly Code code;
		readonly TupleType tupleType;
		readonly bool onlySAE;

		public OpCodeHandler_EVEX_VkHWIb_er(Register baseReg, Code code, TupleType tupleType, bool onlySAE) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			baseReg3 = baseReg;
			this.code = code;
			this.tupleType = tupleType;
			this.onlySAE = onlySAE;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg1;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg2;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp2Kind = OpKind.Register;
				instruction.InternalOp2Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg3;
				if ((state.flags & StateFlags.b) != 0) {
					if (onlySAE)
						instruction.InternalSetSuppressAllExceptions();
					else {
						Debug.Assert((int)RoundingControl.None == 0);
						Debug.Assert((int)RoundingControl.RoundToNearest == 1);
						Debug.Assert((int)RoundingControl.RoundDown == 2);
						Debug.Assert((int)RoundingControl.RoundUp == 3);
						Debug.Assert((int)RoundingControl.RoundTowardZero == 4);
						instruction.InternalRoundingControl = (uint)state.vectorLength + 1;
					}
				}
			}
			else {
				instruction.InternalOp2Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOp3Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadIb();
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_KkHW : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_KkHW(Register baseReg, Code code, TupleType tupleType) {
			this.baseReg = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)state.reg + Register.K0;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp2Kind = OpKind.Register;
				instruction.InternalOp2Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg;
				if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (state.flags & StateFlags.b) != 0)
					decoder.SetInvalidInstruction();
			}
			else {
				instruction.InternalOp2Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOpMask = state.aaa;
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (((uint)(state.flags & StateFlags.z) | state.extraRegisterBase | state.extraRegisterBaseEVEX) != 0 || (state.reg == state.aaa && state.aaa != 0)))
				decoder.SetInvalidInstruction();
		}
	}

	sealed class OpCodeHandler_EVEX_KP1HW : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_KP1HW(Register baseReg, Code code, TupleType tupleType) {
			this.baseReg = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)state.reg + Register.K0;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp2Kind = OpKind.Register;
				instruction.InternalOp2Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg;
				if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (state.flags & StateFlags.b) != 0)
					decoder.SetInvalidInstruction();
			}
			else {
				instruction.InternalOp2Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.z) | state.aaa | state.extraRegisterBase | state.extraRegisterBaseEVEX) != 0)
				decoder.SetInvalidInstruction();
		}
	}

	sealed class OpCodeHandler_EVEX_KkHWIb : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_KkHWIb(Register baseReg, Code code, TupleType tupleType) {
			this.baseReg = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)state.reg + Register.K0;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp2Kind = OpKind.Register;
				instruction.InternalOp2Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg;
				if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (state.flags & StateFlags.b) != 0)
					decoder.SetInvalidInstruction();
			}
			else {
				instruction.InternalOp2Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOp3Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadIb();
			instruction.InternalOpMask = state.aaa;
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (((uint)(state.flags & StateFlags.z) | state.extraRegisterBase | state.extraRegisterBaseEVEX) != 0 || (state.reg == state.aaa && state.aaa != 0)))
				decoder.SetInvalidInstruction();
		}
	}

	sealed class OpCodeHandler_EVEX_WkHV : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_WkHV(Register baseReg, Code code, TupleType tupleType) {
			this.baseReg = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			instruction.InternalCode = code;

			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp0Kind = OpKind.Register;
				instruction.InternalOp0Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg;
				if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (state.flags & StateFlags.b) != 0)
					decoder.SetInvalidInstruction();
			}
			else {
				instruction.InternalOp0Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp2Kind = OpKind.Register;
			instruction.InternalOp2Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg;
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_VHWIb : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_VHWIb(Register baseReg, Code code, TupleType tupleType) {
			this.baseReg = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.b) | state.aaa) != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp2Kind = OpKind.Register;
				instruction.InternalOp2Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg;
			}
			else {
				instruction.InternalOp2Kind = OpKind.Memory;
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOp3Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadIb();
		}
	}

	sealed class OpCodeHandler_EVEX_VHW : OpCodeHandlerModRM {
		readonly Register baseReg1;
		readonly Register baseReg2;
		readonly Register baseReg3;
		readonly Code codeR;
		readonly Code codeM;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_VHW(Register baseReg, Code codeR, Code codeM, TupleType tupleType) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			baseReg3 = baseReg;
			this.codeR = codeR;
			this.codeM = codeM;
			this.tupleType = tupleType;
		}

		public OpCodeHandler_EVEX_VHW(Register baseReg, Code code, TupleType tupleType) {
			baseReg1 = baseReg;
			baseReg2 = baseReg;
			baseReg3 = baseReg;
			codeR = code;
			codeM = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.b) | state.aaa) != 0)
				decoder.SetInvalidInstruction();

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg1;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg2;
			if (state.mod == 3) {
				instruction.InternalCode = codeR;
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp2Kind = OpKind.Register;
				instruction.InternalOp2Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg3;
			}
			else {
				instruction.InternalCode = codeM;
				instruction.InternalOp2Kind = OpKind.Memory;
				decoder.ReadOpMem(ref instruction, tupleType);
			}
		}
	}

	sealed class OpCodeHandler_EVEX_VHM : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_VHM(Register baseReg, Code code, TupleType tupleType) {
			this.baseReg = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.b) | state.aaa) != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.vvvv + baseReg;
			if (state.mod == 3)
				decoder.SetInvalidInstruction();
			else {
				instruction.InternalOp2Kind = OpKind.Memory;
				decoder.ReadOpMem(ref instruction, tupleType);
			}
		}
	}

	sealed class OpCodeHandler_EVEX_Gv_W_er : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code codeW0;
		readonly Code codeW1;
		readonly TupleType tupleType;
		readonly bool onlySAE;

		public OpCodeHandler_EVEX_Gv_W_er(Register baseReg, Code codeW0, Code codeW1, TupleType tupleType, bool onlySAE) {
			this.baseReg = baseReg;
			this.codeW0 = codeW0;
			this.codeW1 = codeW1;
			this.tupleType = tupleType;
			this.onlySAE = onlySAE;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (state.vvvv | state.aaa | state.extraRegisterBaseEVEX) != 0)
				decoder.SetInvalidInstruction();
			if ((state.flags & StateFlags.W) != 0 && decoder.is64Mode) {
				instruction.InternalCode = codeW1;
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp0Kind = OpKind.Register;
				instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase) + Register.RAX;
			}
			else {
				instruction.InternalCode = codeW0;
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp0Kind = OpKind.Register;
				instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase) + Register.EAX;
			}
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp1Kind = OpKind.Register;
				instruction.InternalOp1Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg;
				if ((state.flags & StateFlags.b) != 0) {
					if (onlySAE)
						instruction.InternalSetSuppressAllExceptions();
					else {
						Debug.Assert((int)RoundingControl.None == 0);
						Debug.Assert((int)RoundingControl.RoundToNearest == 1);
						Debug.Assert((int)RoundingControl.RoundDown == 2);
						Debug.Assert((int)RoundingControl.RoundUp == 3);
						Debug.Assert((int)RoundingControl.RoundTowardZero == 4);
						instruction.InternalRoundingControl = (uint)state.vectorLength + 1;
					}
				}
			}
			else {
				instruction.InternalOp1Kind = OpKind.Memory;
				decoder.ReadOpMem(ref instruction, tupleType);
			}
		}
	}

	sealed class OpCodeHandler_EVEX_VX_Ev : OpCodeHandlerModRM {
		readonly Code code32;
		readonly Code code64;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_VX_Ev(Code code32, Code code64, TupleType tupleType) {
			this.code32 = code32;
			this.code64 = code64;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.b) | state.vvvv | state.aaa | state.extraBaseRegisterBaseEVEX) != 0)
				decoder.SetInvalidInstruction();
			Register gpr;
			if ((state.flags & StateFlags.W) != 0 && decoder.is64Mode) {
				instruction.InternalCode = code64;
				gpr = Register.RAX;
			}
			else {
				instruction.InternalCode = code32;
				gpr = Register.EAX;
			}
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + Register.XMM0;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp1Kind = OpKind.Register;
				instruction.InternalOp1Register = (int)(state.rm + state.extraBaseRegisterBase) + gpr;
			}
			else {
				instruction.InternalOp1Kind = OpKind.Memory;
				decoder.ReadOpMem(ref instruction, tupleType);
			}
		}
	}

	sealed class OpCodeHandler_EVEX_Ev_VX : OpCodeHandlerModRM {
		readonly Code code32;
		readonly Code code64;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_Ev_VX(Code code32, Code code64, TupleType tupleType) {
			this.code32 = code32;
			this.code64 = code64;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.b) | state.vvvv | state.aaa | state.extraBaseRegisterBaseEVEX) != 0)
				decoder.SetInvalidInstruction();
			Register gpr;
			if ((state.flags & StateFlags.W) != 0 && decoder.is64Mode) {
				instruction.InternalCode = code64;
				gpr = Register.RAX;
			}
			else {
				instruction.InternalCode = code32;
				gpr = Register.EAX;
			}
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp0Kind = OpKind.Register;
				instruction.InternalOp0Register = (int)(state.rm + state.extraBaseRegisterBase) + gpr;
			}
			else {
				instruction.InternalOp0Kind = OpKind.Memory;
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + Register.XMM0;
		}
	}

	sealed class OpCodeHandler_EVEX_Ev_VX_Ib : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code32;
		readonly Code code64;
		readonly TupleType tupleType;
		readonly bool noMemOp;

		public OpCodeHandler_EVEX_Ev_VX_Ib(Register baseReg, Code code32, Code code64) {
			this.baseReg = baseReg;
			this.code32 = code32;
			this.code64 = code64;
			tupleType = 0;
			noMemOp = true;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.b) | state.vvvv | state.aaa | state.extraRegisterBaseEVEX) != 0)
				decoder.SetInvalidInstruction();
			Register gpr;
			if ((state.flags & StateFlags.W) != 0 && decoder.is64Mode) {
				instruction.InternalCode = code64;
				gpr = Register.RAX;
			}
			else {
				instruction.InternalCode = code32;
				gpr = Register.EAX;
			}
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase) + gpr;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp1Kind = OpKind.Register;
				instruction.InternalOp1Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg;
			}
			else {
				instruction.InternalOp1Kind = OpKind.Memory;
				decoder.ReadOpMem(ref instruction, tupleType);
				if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && noMemOp)
					decoder.SetInvalidInstruction();
			}
			instruction.InternalOp2Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadByte();
		}
	}

	sealed class OpCodeHandler_EVEX_MV : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_MV(Register baseReg, Code code, TupleType tupleType) {
			this.baseReg = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.b) | state.vvvv | state.aaa) != 0)
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			if (state.mod == 3)
				decoder.SetInvalidInstruction();
			else {
				instruction.InternalOp0Kind = OpKind.Memory;
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg;
		}
	}

	sealed class OpCodeHandler_EVEX_VkEv_REXW : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code32;
		readonly Code code64;

		public OpCodeHandler_EVEX_VkEv_REXW(Register baseReg, Code code32) {
			this.baseReg = baseReg;
			this.code32 = code32;
			code64 = Code.INVALID;
		}

		public OpCodeHandler_EVEX_VkEv_REXW(Register baseReg, Code code32, Code code64) {
			this.baseReg = baseReg;
			this.code32 = code32;
			this.code64 = code64;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.b) | state.vvvv | state.extraBaseRegisterBaseEVEX) != 0)
				decoder.SetInvalidInstruction();
			Register gpr;
			if ((state.flags & StateFlags.W) != 0 && decoder.is64Mode) {
				instruction.InternalCode = code64;
				if (code64 == Code.INVALID)
					decoder.SetInvalidInstruction();
				gpr = Register.RAX;
			}
			else {
				instruction.InternalCode = code32;
				gpr = Register.EAX;
			}

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp1Kind = OpKind.Register;
				instruction.InternalOp1Register = (int)(state.rm + state.extraBaseRegisterBase) + gpr;
			}
			else
				decoder.SetInvalidInstruction();
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}

	sealed class OpCodeHandler_EVEX_Vk_VSIB : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Register vsibBase;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_Vk_VSIB(Register baseReg, Register vsibBase, Code code, TupleType tupleType) {
			this.baseReg = baseReg;
			this.vsibBase = vsibBase;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (((state.vvvv & 0xF) | (uint)(state.flags & StateFlags.z)) != 0 || state.aaa == 0))
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			instruction.InternalOpMask = state.aaa;
			int regNum = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX);
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = regNum + baseReg;
			if (state.mod == 3)
				decoder.SetInvalidInstruction();
			else {
				instruction.InternalOp1Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem_VSIB(ref instruction, vsibBase, tupleType);
				if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0) {
					if ((uint)regNum == ((uint)(instruction.MemoryIndex - Register.XMM0) % (uint)InstructionInfoConstants.VMM_count))
						decoder.SetInvalidInstruction();
				}
			}
		}
	}

	sealed class OpCodeHandler_EVEX_VSIB_k1_VX : OpCodeHandlerModRM {
		readonly Register vsibIndex;
		readonly Register baseReg;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_VSIB_k1_VX(Register vsibIndex, Register baseReg, Code code, TupleType tupleType) {
			this.vsibIndex = vsibIndex;
			this.baseReg = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (((uint)(state.flags & (StateFlags.b | StateFlags.z)) | (state.vvvv & 0xF)) != 0 || state.aaa == 0))
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			instruction.InternalOpMask = state.aaa;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg;
			if (state.mod == 3)
				decoder.SetInvalidInstruction();
			else {
				instruction.InternalOp0Kind = OpKind.Memory;
				decoder.ReadOpMem_VSIB(ref instruction, vsibIndex, tupleType);
			}
		}
	}

	sealed class OpCodeHandler_EVEX_VSIB_k1 : OpCodeHandlerModRM {
		readonly Register vsibIndex;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_VSIB_k1(Register vsibIndex, Code code, TupleType tupleType) {
			this.vsibIndex = vsibIndex;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && (((uint)(state.flags & StateFlags.b) | (state.vvvv & 0xF) | state.extraRegisterBase | state.extraRegisterBaseEVEX) != 0 || (state.reg == state.aaa && state.aaa != 0)))
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			instruction.InternalOpMask = state.aaa;
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)state.reg + Register.K0;
			if (state.mod == 3)
				decoder.SetInvalidInstruction();
			else {
				instruction.InternalOp0Kind = OpKind.Memory;
				decoder.ReadOpMem_VSIB(ref instruction, vsibIndex, tupleType);
			}
		}
	}

	sealed class OpCodeHandler_EVEX_GvM_VX_Ib : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code32;
		readonly Code code64;
		readonly TupleType tupleType32;
		readonly TupleType tupleType64;

		public OpCodeHandler_EVEX_GvM_VX_Ib(Register baseReg, Code code32, Code code64, TupleType tupleType32, TupleType tupleType64) {
			this.baseReg = baseReg;
			this.code32 = code32;
			this.code64 = code64;
			this.tupleType32 = tupleType32;
			this.tupleType64 = tupleType64;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((uint)(state.flags & StateFlags.b) | state.vvvv | state.aaa | state.extraBaseRegisterBaseEVEX) != 0)
				decoder.SetInvalidInstruction();
			Register gpr;
			if ((state.flags & StateFlags.W) != 0 && decoder.is64Mode) {
				instruction.InternalCode = code64;
				gpr = Register.RAX;
			}
			else {
				instruction.InternalCode = code32;
				gpr = Register.EAX;
			}
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp0Kind = OpKind.Register;
				instruction.InternalOp0Register = (int)(state.rm + state.extraBaseRegisterBase) + gpr;
			}
			else {
				instruction.InternalOp0Kind = OpKind.Memory;
				if ((state.flags & StateFlags.W) != 0 && decoder.is64Mode)
					decoder.ReadOpMem(ref instruction, tupleType64);
				else
					decoder.ReadOpMem(ref instruction, tupleType32);
			}
			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp1Kind = OpKind.Register;
			instruction.InternalOp1Register = (int)(state.reg + state.extraRegisterBase + state.extraRegisterBaseEVEX) + baseReg;
			instruction.InternalOp2Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadByte();
		}
	}

	sealed class OpCodeHandler_EVEX_KkWIb : OpCodeHandlerModRM {
		readonly Register baseReg;
		readonly Code code;
		readonly TupleType tupleType;

		public OpCodeHandler_EVEX_KkWIb(Register baseReg, Code code, TupleType tupleType) {
			this.baseReg = baseReg;
			this.code = code;
			this.tupleType = tupleType;
		}

		public override void Decode(Decoder decoder, ref Instruction instruction) {
			ref var state = ref decoder.state;
			Debug.Assert(state.Encoding == EncodingKind.EVEX);
			if ((decoder.options & DecoderOptions.NoInvalidCheck) == 0 && ((state.vvvv | state.extraRegisterBase | state.extraRegisterBaseEVEX) != 0 || (state.reg == state.aaa && state.aaa != 0)))
				decoder.SetInvalidInstruction();
			instruction.InternalCode = code;

			Debug.Assert(OpKind.Register == 0);
			//instruction.InternalOp0Kind = OpKind.Register;
			instruction.InternalOp0Register = (int)state.reg + Register.K0;
			if (state.mod == 3) {
				Debug.Assert(OpKind.Register == 0);
				//instruction.InternalOp1Kind = OpKind.Register;
				instruction.InternalOp1Register = (int)(state.rm + state.extraBaseRegisterBase + state.extraBaseRegisterBaseEVEX) + baseReg;
			}
			else {
				instruction.InternalOp1Kind = OpKind.Memory;
				if ((state.flags & StateFlags.b) != 0)
					instruction.SetIsBroadcast();
				decoder.ReadOpMem(ref instruction, tupleType);
			}
			instruction.InternalOp2Kind = OpKind.Immediate8;
			instruction.InternalImmediate8 = decoder.ReadIb();
			instruction.InternalOpMask = state.aaa;
			if ((state.flags & StateFlags.z) != 0)
				instruction.InternalSetZeroingMasking();
		}
	}
}
#endif