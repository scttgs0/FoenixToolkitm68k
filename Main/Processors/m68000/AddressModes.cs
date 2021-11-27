namespace FoenixCore.Processor.m68000
{
    public enum AddressModes
    {
        DataRegisterDirect,                         // Dn
        AddressRegisterDirect,                      // An

        AddressRegisterIndirect,                    // (An)
        AddressRegisterIndirectPostincrement,       // (An)+
        AddressRegisterIndirectPredecrement,        // –(An)
        AddressRegisterIndirectDisplacement,        // (d_16,An)

        AddressRegisterIndirectIndex8bit,           // (d_8,An,Xn)
        AddressRegisterIndirectIndexBase,           // (bd,An,Xn)

        MemoryIndirectPostindexed,                  // ([bd,An],Xn,od)
        MemoryIndirectPreindexed,                   // ([bd,An,Xn],od)

        ProgramCounterIndirectDisplacement,         // (d_16 ,PC)

        ProgramCounterIndirectIndex8bit,            // (d_8,PC,Xn)
        ProgramCounterIndirectIndexBase,            // (bd,PC,Xn)

        ProgramCounterMemoryIndirectPostindexed,    // ([bd,PC],Xn,od)
        ProgramCounterMemoryIndirectPreindexed,     // ([bd,PC,Xn],od)

        AbsoluteShort,                              // (xxx).W
        AbsoluteLong,                               // (xxx).L

        Immediate                                   // #<xxx>
    }
}
