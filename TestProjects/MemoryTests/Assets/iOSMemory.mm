#import <malloc/malloc.h>
#import <mach/mach.h>

extern "C" int _iOSGetTotalNativeMemoryMB()
{
    task_vm_info_data_t vmInfo;
    mach_msg_type_number_t count = TASK_VM_INFO_COUNT;
    kern_return_t kernelReturn = task_info(mach_task_self(), TASK_VM_INFO, (task_info_t)&vmInfo, &count);
    return (int)((CGFloat)vmInfo.phys_footprint / (1024.0f * 1024.0f));
}
