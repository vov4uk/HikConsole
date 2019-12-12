﻿namespace HikConsole.Abstraction
{
    public interface IDirectoryHelper
    {
        long GetTotalFreeSpace(string destination);

        long DirSize(string path);
    }
}
