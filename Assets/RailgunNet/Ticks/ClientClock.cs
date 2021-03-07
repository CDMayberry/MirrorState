
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
    public class ClientClock
    {
        public uint TimerTick = 1;
        private ITickable _tickable;
        // This will be essentially 'How many Tick calls before this calls _ticker.Tick()?'
        public uint SendRate = 1;
        private uint _nextTick = 0;

        public ClientClock(ITickable tickable)
        {
            _tickable = tickable;
            _nextTick = SendRate;
        }

        // Typically called in FixedUpdate.
        public void Tick()
        {
            TimerTick += 1;
            if (TimerTick >= _nextTick)
            {
                _tickable.SendTick();
                _nextTick = TimerTick + SendRate;
            }
        }
    }
}