using System.Threading.Tasks;

namespace IsikReg.Extensions {
  public static class TaskExtension {

    public static Task OnExceptionQuit(this Task task) {
      task.ContinueWith(task => {
        if (task.IsFaulted && task.Exception != null) {
          App.ShowException(task.Exception.GetBaseException());
        }
      }, TaskContinuationOptions.OnlyOnFaulted);
      return task;
    }

    public static Task<T> OnExceptionQuit<T>(this Task<T> task) {
      task.ContinueWith(task => {
        if (task.IsFaulted && task.Exception != null) {
          App.ShowException(task.Exception.GetBaseException());
        }
      }, TaskContinuationOptions.OnlyOnFaulted);
      return task;
    }

  }
}
