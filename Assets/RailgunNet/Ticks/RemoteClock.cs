
/*
 *  RailgunNet - A Client/Server Network State-Synchronization Layer for Games
 *  Copyright (c) 2016 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *  
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
*/

using RailgunNet.Ticks.Interfaces;

namespace RailgunNet.Ticks
{

    /// <summary>
    /// Used for keeping track of the remote client's clock on the Server instance.
    /// </summary>
    public class RemoteClock
    {
        public const int TicksPerSecond = 50;
        public const int FramesPerTick = 1; // Assuming fixed update
        public const double UpdateFrequency = 1d / TicksPerSecond;

        private const int DELAY_MIN = 3;
        private const int DELAY_MAX = 9;

        private readonly ITickable _tickable;
        private readonly uint _remoteRate;
        private readonly uint _delayDesired;
        private readonly uint _delayMin;
        private readonly uint _delayMax;

        // Here's where I think remote is at.
        public uint EstimatedRemote { get; private set; }
        // Here is what remote has given me.
        public uint LatestRemote { get; private set; }

        // TODO: Move this to a constants package.
        public RemoteClock(ITickable tickable, uint remoteSendRate = FramesPerTick, uint delayMin = DELAY_MIN, uint delayMax = DELAY_MAX)
        {
            _tickable = tickable;

            _remoteRate = remoteSendRate;
            EstimatedRemote = TickConstants.BadTick;
            LatestRemote = TickConstants.BadTick;

            _delayMin = delayMin;
            _delayMax = delayMax;
            _delayDesired = (delayMax - delayMin) / 2 + delayMin;
        }

        public void Update(uint latestTick)
        {
            if (latestTick == TickConstants.BadTick || latestTick < _delayDesired)
            {
                return;
            }

            if (LatestRemote == TickConstants.BadTick)
            {
                LatestRemote = latestTick;
                EstimatedRemote = LatestRemote - _delayDesired;
            }

            /*if (EstimatedRemote == TickConstants.BadTick)
            {
                EstimatedRemote = LatestRemote - _delayDesired;
            }*/

            if (latestTick > LatestRemote)
            {
                LatestRemote = latestTick;
                UpdateEstimate();
                _tickable.SendTick();
            }
        }

        // See http://www.gamedev.net/topic/652186-de-jitter-buffer-on-both-the-client-and-server/
        public void UpdateEstimate()
        {
            EstimatedRemote += 1;
            if (LatestRemote == TickConstants.BadTick)
            {
                return;
            }

            uint delta = LatestRemote - EstimatedRemote;

            if (ShouldSnapTick(delta))
            {
                // Reset
                //RailDebug.LogMessage("Reset");
                EstimatedRemote = LatestRemote - _delayDesired;
            }
            else if (delta > _delayMax)
            {
                // Jump 1
                //RailDebug.LogMessage("Jump 1");
                EstimatedRemote += 1;
            }
            else if (delta < _delayMin)
            {
                // Stall 1
                //RailDebug.LogMessage("Stall 1");
                EstimatedRemote -= 1;
            }
        }

        private bool ShouldSnapTick(float delta)
        {
            if (delta < _delayMin - _remoteRate)
                return true;
            if (delta > _delayMax + _remoteRate)
                return true;
            return false;
        }
    }
}