﻿// This file come from below URL:
// https://github.com/android/platform_frameworks_support/blob/62cf5e32ad0d24fffde4c0d0425aa12cd2b054a6/v4/java/android/support/v4/app/FragmentStatePagerAdapter.java
// Contains this patch: https://code.google.com/p/android/issues/detail?id=77285
//
// Modified to keep fragment state by item id instead of position
// - Refer https://code.google.com/p/android/issues/detail?id=37990
// - Also refer FragmentPagerAdapter.java
// <auto-generated/>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Support.V4.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.View;
using Android.Views;
using Android.Widget;
using Java.Lang;
using Object = System.Object;
using String = System.String;

namespace CarouselView.FormsPlugin.Android
{
    

/**
 * Implementation of {@link android.support.v4.view.PagerAdapter} that
 * uses a {@link Fragment} to manage each page. This class also handles
 * saving and restoring of fragment's state.
 * <p>
 * <p>This version of the pager is more useful when there are a large number
 * of pages, working more like a list view.  When pages are not visible to
 * the user, their entire fragment may be destroyed, only keeping the saved
 * state of that fragment.  This allows the pager to hold on to much less
 * memory associated with each visited page as compared to
 * {@link FragmentPagerAdapter} at the cost of potentially more overhead when
 * switching between pages.
 * <p>
 * <p>When using FragmentPagerAdapter the host ViewPager must have a
 * valid ID set.</p>
 * <p>
 * <p>Subclasses only need to implement {@link #getItem(int)}
 * and {@link #getCount()} to have a working adapter.
 * <p>
 * <p>Here is an example implementation of a pager containing fragments of
 * lists:
 * <p>
 * {@sample development/samples/Support13Demos/src/com/example/android/supportv13/app/FragmentStatePagerSupport.java
 * complete}
 * <p>
 * <p>The <code>R.layout.fragment_pager</code> resource of the top-level fragment is:
 * <p>
 * {@sample development/samples/Support13Demos/res/layout/fragment_pager.xml
 * complete}
 * <p>
 * <p>The <code>R.layout.fragment_pager_list</code> resource containing each
 * individual fragment's layout is:
 * <p>
 * {@sample development/samples/Support13Demos/res/layout/fragment_pager_list.xml
 * complete}
 */
public abstract class FragmentItemIdStatePagerAdapter : PagerAdapter {
    private const String TAG = "FragmentItemIdAdapter";
    private const String KEY_FRAGMENT = "fragment";
    private const bool DEBUG = false;

    private readonly FragmentManager mFragmentManager;
    private FragmentTransaction mCurTransaction = null;

    private Dictionary<long, Fragment.SavedState> mSavedState = new Dictionary<long, Fragment.SavedState>();
    private Dictionary<Fragment, long> mFragmentToItemIdMap = new Dictionary<Fragment, long>();
    private Dictionary<long, Fragment> mItemIdToFragmentMap = new Dictionary<long, Fragment>();
    private HashSet<Fragment> mUnusedRestoredFragments = new HashSet<Fragment>();
    private JniWeakReference<Fragment> mWeakCurrentPrimaryItem = null;

    protected FragmentItemIdStatePagerAdapter(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected FragmentItemIdStatePagerAdapter(FragmentManager fm) {
        mFragmentManager = fm;
    }

    /**
     * Return the Fragment associated with a specified position.
     */
    public abstract Fragment GetItem(int position);

    /**
     * Return a unique identifier for the item at the given position.
     * <p>
     * <p>The default implementation returns the given position.
     * Subclasses should override this method if the positions of items can change.</p>
     *
     * @param position Position within this adapter
     * @return Unique identifier for the item at position
     */
    public virtual long GetItemId(int position) {
        return position;
    }

    public override void StartUpdate(ViewGroup container) {
    }

    public override Java.Lang.Object InstantiateItem(ViewGroup container, int position) {
        long itemId = GetItemId(position);

        // If we already have this item instantiated, there is nothing
        // to do.  This can happen when we are restoring the entire pager
        // from its saved state, where the fragment manager has already
        // taken care of restoring the fragments we previously had instantiated.
        if (mItemIdToFragmentMap.TryGetValue(itemId,out var f))
        {
            mUnusedRestoredFragments.Remove(f);
            return f;
        }

        if (mCurTransaction == null) {
            mCurTransaction = mFragmentManager.BeginTransaction();
        }

        Fragment fragment = GetItem(position);
        mFragmentToItemIdMap.Add(fragment, itemId);
        mItemIdToFragmentMap.Add(itemId, fragment);
        if (mSavedState.TryGetValue(itemId, out var fss))
        {
            fragment.SetInitialSavedState(fss);
        }
        fragment.SetMenuVisibility(false);
        fragment.UserVisibleHint = false;
        mCurTransaction.Add(container.Id, fragment);

        return fragment;
    }

    public override void DestroyItem(ViewGroup container, int position, Java.Lang.Object @object) {
        Fragment fragment = (Fragment)@object;
        destroyFragment(fragment);
    }

    private void destroyFragment(Fragment fragment) {
        if (mCurTransaction == null) {
            mCurTransaction = mFragmentManager.BeginTransaction();
        }

        if (mFragmentToItemIdMap.TryGetValue(fragment, out var itemId))
        {
            mFragmentToItemIdMap.Remove(fragment);
            mItemIdToFragmentMap.Remove(itemId);
        }
        else
        {
            // XXX: Workaround for NullPointerException, but I don't know why ViewPager passes fragment
            // which is not owned by pager adapter (i.e. mFragmentToItemIdMap does not contain it).
            mSavedState.Add(itemId, mFragmentManager.SaveFragmentInstanceState(fragment));
        }

        mCurTransaction.Remove(fragment);
    }
    
    public override void SetPrimaryItem(ViewGroup container, int position, Java.Lang.Object @object) {
        Fragment fragment = (Fragment)@object;

        var mCurrentPrimaryItem = mWeakCurrentPrimaryItem?.GetTarget();
        if (fragment != mCurrentPrimaryItem) {
            if (mCurrentPrimaryItem != null) {
                mCurrentPrimaryItem.SetMenuVisibility(false);
                mCurrentPrimaryItem.UserVisibleHint = false;
            }
            if (fragment != null) {
                fragment.SetMenuVisibility(true);
                fragment.UserVisibleHint = true;
            }
            mWeakCurrentPrimaryItem = new JniWeakReference<Fragment>(fragment);
        }
    }

    public override void FinishUpdate(ViewGroup container) {
        if (mUnusedRestoredFragments.Count > 0) {
            // Remove fragments which are restored but unused after first finishUpdate.
            foreach (Fragment fragment in mUnusedRestoredFragments) {
                destroyFragment(fragment);
            }
            mUnusedRestoredFragments.Clear();
        }
        if (mCurTransaction != null) {
            mCurTransaction.CommitAllowingStateLoss();
            mCurTransaction = null;
            mFragmentManager.ExecutePendingTransactions();
        }
    }
   
    public override bool IsViewFromObject(View view, Java.Lang.Object @object) {
        return ((Fragment)@object).View == view;
    }

    public override IParcelable SaveState() {
        Bundle state = null;
        if (mSavedState.Count > 0) {
            state = new Bundle();
            long[] itemIdsForState = new long[mSavedState.Count];
            Fragment.SavedState[] fss = new Fragment.SavedState[mSavedState.Count];
            int i = 0;
            foreach (var savedStateEntry in mSavedState) {
                itemIdsForState[i] = savedStateEntry.Key;
                fss[i] = savedStateEntry.Value;
                i++;
            }
            state.PutLongArray("itemIdsForState", itemIdsForState);
            state.PutParcelableArray("states", fss);
        }
        foreach (var fragmentToIdEntry in mFragmentToItemIdMap) {
            Fragment f = fragmentToIdEntry.Key;
            if (f != null && f.IsAdded) {
                if (state == null) {
                    state = new Bundle();
                }
                long itemId = fragmentToIdEntry.Value;
                String bundleKey = KEY_FRAGMENT + itemId;
                mFragmentManager.PutFragment(state, bundleKey, f);
            }
        }
        return state;
    }

    public override void RestoreState(IParcelable state, ClassLoader loader) {
        if (state != null) {
            Bundle bundle = (Bundle) state;
            bundle.SetClassLoader(loader);
            long[] itemIdsForState = bundle.GetLongArray("itemIdsForState");
            IParcelable[] fss = bundle.GetParcelableArray("states");
            mFragmentToItemIdMap.Clear();
            mItemIdToFragmentMap.Clear();
            mUnusedRestoredFragments.Clear();
            mSavedState.Clear();
            if (fss != null) {
                for (int i = 0; i < fss.Length; i++) {
                    mSavedState.Add(itemIdsForState[i], (Fragment.SavedState) fss[i]);
                }
            }
            var keys = bundle.KeySet();
            foreach (String key in keys) {
                if (key.StartsWith(KEY_FRAGMENT)) {
                    long itemId = long.Parse(key.Substring(KEY_FRAGMENT.Length));
                    Fragment f = mFragmentManager.GetFragment(bundle, key);
                    if (f != null) {
                        f.SetMenuVisibility(false);
                        mFragmentToItemIdMap.Add(f, itemId);
                        mItemIdToFragmentMap.Add(itemId, f);
                    } else {
                        System.Diagnostics.Debug.WriteLine("Bad fragment at key " + key);
                    }
                }
            }
            mUnusedRestoredFragments = new HashSet<Fragment>(mFragmentToItemIdMap.Keys);
        }
    }
}
}