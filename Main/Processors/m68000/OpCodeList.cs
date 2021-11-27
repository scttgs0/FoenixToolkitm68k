using System.Collections.Generic;


namespace FoenixCore.Processor.m68000
{
    public class OpcodeList : List<OpCode>
    {
        #region constants
        #endregion constants

        public OpcodeList(Operations operations, CentralProcessingUnit cpu)
        {
// ABCD
// ADD
// ADDA
// ADDI
// ADDQ
// ADDX
// AND
// ANDI
// ANDI to CCR
// ANDI to SR
// ASL
// ASR
// Bcc
// BCHG
// BCLR
// BFCHG
// BFCLR
//      BFEXTS
//      BFEXTU
//      BFFFO
//      BFINS
//      BFSET
//      BFTST
//      BGND
//      BKPT
// BRA
// BSET
// BSR
// BTST
//      CALLM
//      CAS
//      CAS2
// CHK
//      CHK2
//      CINV
// CLR
// CMP
// CMPA
// CMPI
// CMPM
//      CMP2
//      cpBcc
//      cpDBcc
//      cpGEN
//      cpRESTORE
//      cpSAVE
//      cpScc
//      cpTRAPcc
//      CPUSH
// DBcc
// DIVS
//      DIVSL
// DIVU
//      DIVUL
// EOR
// EORI
// EORI to CCR
// EORI to SR
// EXG
// EXT
//      EXTB
//      FABS
//      FSFABS
//      FDFABS
//      FACOS
//      FADD
//      FSADD
//      FDADD
//      FASIN
//      FATAN
//      FATANH
//      FBcc
//      FCMP
//      FCOS
//      FCOSH
//      FDBcc
//      FDIV
//      FSDIV
//      FDDIV
//      FETOX
//      FETOXM1
//      FGETEXP
//      FGETMAN
//      FINT
//      FINTRZ
//      FLOG10
//      FLOG2
//      FLOGN
//      FLOGNP1
//      FMOD
//      FMOVE
//      FSMOVE
//      FDMOVE
//      FMOVECR
//      FMOVEM
//      FMUL
//      FSMUL
//      FDMUL
//      FNEG
//      FSNEG
//      FDNEG
//      FNOP
//      FREM
//      FRESTORE
//      FSAVE
//      FSCALE
//      FScc
//      FSGLDIV
//      FSGLMUL
//      FSIN
//      FSINCOS
//      FSINH
//      FSQRT
//      FSSQRT
//      FDSQRT
//      FSUB
//      FSSUB
//      FDSUB
//      FTAN
//      FTANH
//      FTENTOX
//      FTRAPcc
//      FTST
//      FTWOTOX
// ILLEGAL
// JMP
// JSR
// LEA
// LINK
//      LPSTOP
// LSL
// LSR
// MOVE
//      MOVE from CCR
// MOVE from SR
// MOVE to CCR
// MOVE to SR
// MOVE USP
//      MOVE16
// MOVEA
//      MOVEC
// MOVEM
// MOVEP
// MOVEQ
//      MOVES
// MULS
// MULU
// NBCD
// NEG
// NEGX
// NOP
// NOT
// OR
// ORI
// ORI to CCR
// ORI to SR
//      PACK
//      PBcc
//      PDBcc
// PEA
//      PFLUSH
//      PFLUSHA
//      PFLUSHR
//      PFLUSHS
//      PLOAD
//      PMOVE
//      PRESTORE
//      PSAVE
//      PScc
//      PTEST
//      PTRAPcc
//      PVALID
// RESET
// ROL
// ROR
// ROXL
// ROXR
//      RTD
// RTE
//      RTM
// RTR
// RTS
// SBCD
// Scc
// STOP
// SUB
// SUBA
// SUBI
// SUBQ
// SUBX
// SWAP
// TAS
//      TBLS
//      TBLSN
//      TBLU
//      TBLUN
// TRAP
//      TRAPcc
// TRAPV
// TST
// UNLK
//      UNPK
        }
    }
}
